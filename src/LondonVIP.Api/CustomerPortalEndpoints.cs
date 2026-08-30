using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Security;
using LondonVIP.Shared.CustomerPortal;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Security;
using LondonVIP.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LondonVIP.Api;

public static class CustomerPortalEndpoints
{
    public static IEndpointRouteBuilder MapCustomerPortalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/customer-portal")
            .RequireAuthorization(SecurityPolicies.CustomerRead)
            .RequireRateLimiting("operations");
        group.MapGet("/customers", GetCustomersAsync);
        group.MapGet("/{customerId:guid}", GetDashboardAsync);
        group.MapGet("/{customerId:guid}/bookings/{bookingId:guid}", GetBookingAsync);
        return endpoints;
    }

    private static async Task<IResult> GetCustomersAsync(LondonVIPDbContext db, ICompanyContext company, CancellationToken token)
    {
        var customers = await db.Customers.AsNoTracking()
            .Where(customer => customer.CompanyId == company.CompanyId && customer.IsActive)
            .Select(customer => new CustomerPortalCustomerDto
            {
                Id = customer.Id,
                FullName = customer.FirstName + " " + customer.LastName,
                Email = customer.Email
            }).ToListAsync(token);
        return Results.Ok(customers.OrderBy(customer => customer.FullName));
    }

    private static async Task<IResult> GetDashboardAsync(Guid customerId, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, CancellationToken token)
    {
        var customer = await db.Customers.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == customerId && item.CompanyId == company.CompanyId, token);
        if (customer is null)
        {
            await AuditCrossTenantAsync(db, audit, customerId, company.CompanyId, token);
            return Results.NotFound();
        }

        var bookings = await db.Bookings.AsNoTracking()
            .Where(item => item.CompanyId == company.CompanyId && item.CustomerId == customerId)
            .Select(item => new CustomerPortalBookingDto
            {
                Id = item.Id,
                BookingReference = item.BookingReference,
                PickupDateTime = item.PickupDateTime,
                PickupAddress = item.PickupAddress,
                Destination = item.Destination,
                Status = item.Status.ToString(),
                PaymentStatus = item.PaymentStatus,
                VehicleType = item.VehicleType.ToString(),
                TotalFare = item.TotalFare,
                DriverName = item.Driver == null ? null : item.Driver.FirstName + " " + item.Driver.LastName,
                FlightNumber = item.FlightNumber,
                InvoiceNumber = db.InvoiceLines.Where(line => line.BookingId == item.Id && line.Invoice.CompanyId == company.CompanyId)
                    .Select(line => line.Invoice.InvoiceNumber).FirstOrDefault()
            }).ToListAsync(token);

        var invoices = await db.Invoices.AsNoTracking()
            .Where(item => item.CompanyId == company.CompanyId && item.CustomerId == customerId)
            .Select(item => new CustomerPortalInvoiceDto
            {
                Id = item.Id, InvoiceNumber = item.InvoiceNumber, InvoiceDate = item.InvoiceDate, DueDate = item.DueDate,
                Status = item.Status.ToString(), TotalAmount = item.TotalAmount, AmountPaid = item.AmountPaid, BalanceDue = item.BalanceDue
            }).ToListAsync(token);

        var payments = await db.Payments.AsNoTracking()
            .Where(item => item.CompanyId == company.CompanyId && item.CustomerId == customerId)
            .Select(item => new CustomerPortalPaymentDto
            {
                Id = item.Id, PaymentReference = item.PaymentReference, PaymentDate = item.PaymentDate,
                PaymentMethod = item.PaymentMethod.ToString(), Amount = item.Amount,
                AllocatedAmount = item.Allocations.Sum(allocation => allocation.Amount)
            }).ToListAsync(token);

        await audit.WriteAsync("CustomerPortalViewed", "CustomerPortal", "Succeeded", SecurityEventSeverity.Information,
            "Customer portal account summary viewed.", "Customer", customerId.ToString(), company.CompanyId, token);
        return Results.Ok(new CustomerPortalDashboardDto
        {
            CustomerId = customer.Id, CustomerName = customer.FirstName + " " + customer.LastName,
            Email = customer.Email, Phone = customer.Phone,
            Bookings = bookings.OrderByDescending(item => item.PickupDateTime).ToList(),
            Invoices = invoices.OrderByDescending(item => item.InvoiceDate).ToList(),
            Payments = payments.OrderByDescending(item => item.PaymentDate).ToList()
        });
    }

    private static async Task<IResult> GetBookingAsync(Guid customerId, Guid bookingId, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, CancellationToken token)
    {
        var booking = await db.Bookings.AsNoTracking()
            .Where(item => item.Id == bookingId && item.CustomerId == customerId && item.CompanyId == company.CompanyId)
            .Select(item => new CustomerPortalBookingDetailDto
            {
                Booking = new CustomerPortalBookingDto
                {
                    Id = item.Id, BookingReference = item.BookingReference, PickupDateTime = item.PickupDateTime,
                    PickupAddress = item.PickupAddress, Destination = item.Destination, Status = item.Status.ToString(),
                    PaymentStatus = item.PaymentStatus, VehicleType = item.VehicleType.ToString(), TotalFare = item.TotalFare,
                    DriverName = item.Driver == null ? null : item.Driver.FirstName + " " + item.Driver.LastName,
                    FlightNumber = item.FlightNumber,
                    InvoiceNumber = db.InvoiceLines.Where(line => line.BookingId == item.Id && line.Invoice.CompanyId == company.CompanyId)
                        .Select(line => line.Invoice.InvoiceNumber).FirstOrDefault()
                },
                PassengerCount = item.PassengerCount, LuggageCount = item.LuggageCount,
                IsAirportPickup = item.IsAirportPickup, IsMeetAndGreet = item.IsMeetAndGreet, CustomerNotes = item.CustomerNotes
            }).SingleOrDefaultAsync(token);
        if (booking is null)
        {
            await AuditBookingAccessAsync(db, audit, customerId, bookingId, company.CompanyId, token);
            return Results.NotFound();
        }

        await audit.WriteAsync("CustomerPortalBookingViewed", "CustomerPortal", "Succeeded", SecurityEventSeverity.Information,
            "Customer portal booking detail viewed.", "Booking", bookingId.ToString(), company.CompanyId, token);
        return Results.Ok(booking);
    }

    private static async Task AuditCrossTenantAsync(LondonVIPDbContext db, IAuditService audit, Guid customerId, Guid companyId, CancellationToken token)
    {
        if (await db.Customers.AnyAsync(item => item.Id == customerId && item.CompanyId != companyId, token))
            await audit.WriteAsync("CrossTenantAccessAttempt", "Authorization", "Denied", SecurityEventSeverity.High,
                "Cross-tenant customer portal access was blocked.", "Customer", customerId.ToString(), companyId, token);
    }

    private static async Task AuditBookingAccessAsync(LondonVIPDbContext db, IAuditService audit, Guid customerId, Guid bookingId, Guid companyId, CancellationToken token)
    {
        if (await db.Bookings.AnyAsync(item => item.Id == bookingId && (item.CompanyId != companyId || item.CustomerId != customerId), token))
            await audit.WriteAsync("CustomerPortalAccessDenied", "Authorization", "Denied", SecurityEventSeverity.High,
                "Customer portal booking access was blocked.", "Booking", bookingId.ToString(), companyId, token);
    }
}
