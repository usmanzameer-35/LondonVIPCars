using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Security;
using LondonVIP.Shared.Customers;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Security;
using LondonVIP.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LondonVIP.Api;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/customers").RequireRateLimiting("operations");
        group.MapGet("", GetCustomersAsync).RequireAuthorization(SecurityPolicies.CustomerRead);
        group.MapGet("/{id:guid}", GetCustomerAsync).RequireAuthorization(SecurityPolicies.CustomerRead);
        group.MapGet("/{id:guid}/bookings", GetBookingsAsync).RequireAuthorization(SecurityPolicies.CustomerRead);
        group.MapPost("", CreateCustomerAsync).RequireAuthorization(SecurityPolicies.CustomerWrite);
        group.MapPut("/{id:guid}", UpdateCustomerAsync).RequireAuthorization(SecurityPolicies.CustomerWrite);
        return endpoints;
    }

    private static async Task<IResult> GetCustomersAsync(LondonVIPDbContext db, ICompanyContext company, CancellationToken cancellationToken)
    {
        var customers = await db.Customers.AsNoTracking().Where(item => item.CompanyId == company.CompanyId)
            .Select(item => new CustomerListItemDto { Id = item.Id, FirstName = item.FirstName, LastName = item.LastName, Email = item.Email, Phone = item.Phone, Postcode = item.Postcode, IsActive = item.IsActive })
            .ToListAsync(cancellationToken);
        var activity = await LoadBookingActivityAsync(db, company.CompanyId, customers.Select(item => item.Id).ToHashSet(), cancellationToken);
        foreach (var customer in customers)
        {
            var bookings = activity.Where(item => item.CustomerId == customer.Id).ToList();
            customer.TotalBookings = bookings.Count;
            customer.LastBookingDate = bookings.Count == 0 ? null : bookings.Max(item => item.PickupDateTime);
            customer.TotalSpend = bookings.Where(item => item.Status == BookingStatus.Completed).Sum(item => item.TotalFare);
        }
        return Results.Ok(customers.OrderBy(item => item.LastName).ThenBy(item => item.FirstName));
    }

    private static async Task<IResult> GetCustomerAsync(Guid id, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && item.CompanyId == company.CompanyId, cancellationToken);
        if (customer is null) { await AuditCrossTenantAsync(db, audit, id, company.CompanyId, cancellationToken); return Results.NotFound(); }
        var activity = await LoadBookingActivityAsync(db, company.CompanyId, [id], cancellationToken);
        return Results.Ok(ToDetail(customer, Summarize(activity, DateTimeOffset.UtcNow)));
    }

    private static async Task<IResult> GetBookingsAsync(Guid id, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, CancellationToken cancellationToken)
    {
        if (!await db.Customers.AnyAsync(item => item.Id == id && item.CompanyId == company.CompanyId, cancellationToken))
        { await AuditCrossTenantAsync(db, audit, id, company.CompanyId, cancellationToken); return Results.NotFound(); }

        var bookings = await db.Bookings.AsNoTracking().Where(item => item.CompanyId == company.CompanyId && item.CustomerId == id)
            .Select(item => new CustomerBookingHistoryItemDto
            {
                BookingId = item.Id, BookingReference = item.BookingReference, PickupDateTime = item.PickupDateTime,
                PickupAddress = item.PickupAddress, Destination = item.Destination, VehicleType = item.VehicleType,
                Status = item.Status, TotalFare = item.TotalFare, PaymentStatus = item.PaymentStatus,
                DriverName = item.Driver == null ? null : item.Driver.FirstName + " " + item.Driver.LastName
            }).ToListAsync(cancellationToken);
        return Results.Ok(bookings.OrderByDescending(item => item.PickupDateTime));
    }

    private static async Task<IResult> CreateCustomerAsync(CustomerCreateDto request, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, CancellationToken cancellationToken)
    {
        var errors = CustomerValidator.Validate(request);
        await AddDuplicateErrorAsync(errors, request.Email, null, db, company.CompanyId, cancellationToken);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var now = DateTimeOffset.UtcNow;
        var customer = new Customer { Id = Guid.NewGuid(), CompanyId = company.CompanyId, CreatedAt = now, UpdatedAt = now };
        Apply(request, customer); db.Customers.Add(customer); await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("CustomerCreated", "Customer", "Succeeded", SecurityEventSeverity.Information, "Customer record created.", "Customer", customer.Id.ToString(), company.CompanyId, cancellationToken);
        return Results.Created($"/api/customers/{customer.Id}", ToDetail(customer, new CustomerActivitySummaryDto()));
    }

    private static async Task<IResult> UpdateCustomerAsync(Guid id, CustomerUpdateDto request, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, CancellationToken cancellationToken)
    {
        var errors = CustomerValidator.Validate(request);
        await AddDuplicateErrorAsync(errors, request.Email, id, db, company.CompanyId, cancellationToken);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var customer = await db.Customers.SingleOrDefaultAsync(item => item.Id == id && item.CompanyId == company.CompanyId, cancellationToken);
        if (customer is null) { await AuditCrossTenantAsync(db, audit, id, company.CompanyId, cancellationToken); return Results.NotFound(); }
        Apply(request, customer); customer.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("CustomerUpdated", "Customer", "Succeeded", SecurityEventSeverity.Information, "Customer record updated.", "Customer", customer.Id.ToString(), company.CompanyId, cancellationToken);
        var activity = await LoadBookingActivityAsync(db, company.CompanyId, [id], cancellationToken);
        return Results.Ok(ToDetail(customer, Summarize(activity, DateTimeOffset.UtcNow)));
    }

    private static async Task AddDuplicateErrorAsync(Dictionary<string, string[]> errors, string? email, Guid? excludingId, LondonVIPDbContext db, Guid companyId, CancellationToken cancellationToken)
    {
        var normalized = email?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalized)) return;
        if (await db.Customers.AnyAsync(item => item.CompanyId == companyId && item.Id != excludingId && item.Email.ToUpper() == normalized, cancellationToken))
            errors["email"] = ["A customer with this email already exists for the current company."];
    }

    private static void Apply(CustomerCreateDto request, Customer customer)
    {
        customer.FirstName = request.FirstName.Trim(); customer.LastName = request.LastName.Trim();
        customer.Email = request.Email.Trim().ToLowerInvariant(); customer.Phone = request.Phone.Trim();
        customer.SecondaryPhone = Null(request.SecondaryPhone); customer.Address = Null(request.Address);
        customer.Postcode = Null(request.Postcode)?.ToUpperInvariant(); customer.Notes = Null(request.Notes); customer.IsActive = request.IsActive;
    }

    private static CustomerDetailDto ToDetail(Customer item, CustomerActivitySummaryDto activity) => new()
    {
        Id = item.Id, FirstName = item.FirstName, LastName = item.LastName, Email = item.Email, Phone = item.Phone,
        SecondaryPhone = item.SecondaryPhone, Address = item.Address, Postcode = item.Postcode, Notes = item.Notes,
        IsActive = item.IsActive, CreatedAt = item.CreatedAt, UpdatedAt = item.UpdatedAt, Activity = activity
    };

    private static CustomerActivitySummaryDto Summarize(IReadOnlyList<BookingActivity> bookings, DateTimeOffset now) => new()
    {
        TotalBookings = bookings.Count,
        UpcomingBookings = bookings.Count(item => item.PickupDateTime >= now && item.Status is not (BookingStatus.Completed or BookingStatus.Cancelled)),
        CompletedBookings = bookings.Count(item => item.Status == BookingStatus.Completed),
        TotalSpend = bookings.Where(item => item.Status == BookingStatus.Completed).Sum(item => item.TotalFare),
        LastJourney = bookings.Where(item => item.PickupDateTime < now).Select(item => (DateTimeOffset?)item.PickupDateTime).Max()
    };

    private static Task<List<BookingActivity>> LoadBookingActivityAsync(LondonVIPDbContext db, Guid companyId, HashSet<Guid> customerIds, CancellationToken cancellationToken) =>
        db.Bookings.AsNoTracking().Where(item => item.CompanyId == companyId && customerIds.Contains(item.CustomerId))
            .Select(item => new BookingActivity(item.CustomerId, item.PickupDateTime, item.Status, item.TotalFare)).ToListAsync(cancellationToken);

    private static async Task AuditCrossTenantAsync(LondonVIPDbContext db, IAuditService audit, Guid id, Guid companyId, CancellationToken cancellationToken)
    {
        if (await db.Customers.AnyAsync(item => item.Id == id && item.CompanyId != companyId, cancellationToken))
            await audit.WriteAsync("CrossTenantAccessAttempt", "Authorization", "Denied", SecurityEventSeverity.High, "Cross-tenant customer access was blocked.", "Customer", id.ToString(), companyId, cancellationToken);
    }

    private static string? Null(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private sealed record BookingActivity(Guid CustomerId, DateTimeOffset PickupDateTime, BookingStatus Status, decimal TotalFare);
}
