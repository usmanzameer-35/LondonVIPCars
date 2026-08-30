using LondonVIP.Infrastructure.Bookings;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Shared.Bookings;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Tenancy;
using LondonVIP.Infrastructure.Security;
using LondonVIP.Shared.Security;
using LondonVIP.Shared.Invoicing;
using LondonVIP.Shared.Invoices;
using Microsoft.EntityFrameworkCore;
using LondonVIP.Shared.Notifications;

namespace LondonVIP.Api;

public static class BookingEndpoints
{
    public static IEndpointRouteBuilder MapBookingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/bookings").RequireAuthorization(SecurityPolicies.BookingOperations).RequireRateLimiting("operations");
        group.MapGet("", GetBookingsAsync);
        group.MapGet("/lookups", GetLookupsAsync);
        group.MapGet("/{id:guid}", GetBookingAsync);
        group.MapPost("", CreateBookingAsync);
        group.MapPut("/{id:guid}", UpdateBookingAsync);
        group.MapPatch("/{id:guid}/status", UpdateStatusAsync);
        endpoints.MapPost("/api/bookings/{bookingId:guid}/invoice", GenerateInvoiceAsync)
            .RequireAuthorization(SecurityPolicies.FinanceOperations)
            .RequireRateLimiting("operations");
        return endpoints;
    }

    private static async Task<IResult> GenerateInvoiceAsync(Guid bookingId, IBookingInvoiceService service, IAuditService audit, ICompanyContext company,INotificationService notifications,CancellationToken cancellationToken)
    {
        var result = await service.GenerateInvoiceAsync(bookingId, cancellationToken);
        if (result.Outcome == InvoiceGenerationOutcome.NotFound)
        {
            await audit.WriteAsync("BookingInvoiceGenerationRejected", "Invoice", "NotFound", SecurityEventSeverity.Warning, "Booking invoice generation was requested for a missing booking.", "Booking", bookingId.ToString(), company.CompanyId, cancellationToken);
            return Results.NotFound();
        }
        if (result.Outcome == InvoiceGenerationOutcome.ValidationFailure)
        {
            await audit.WriteAsync("BookingInvoiceGenerationRejected", "Invoice", "ValidationFailure", SecurityEventSeverity.Warning, result.Error ?? "Booking is not eligible for invoicing.", "Booking", bookingId.ToString(), company.CompanyId, cancellationToken);
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["booking"] = [result.Error ?? "Booking is not eligible for invoicing."] });
        }

        var invoice = result.Invoice!;
        var alreadyExists = result.Outcome == InvoiceGenerationOutcome.AlreadyExists;
        await audit.WriteAsync(alreadyExists ? "BookingInvoiceAlreadyExists" : "BookingInvoiceCreated", "Invoice", "Succeeded", SecurityEventSeverity.Information,
            alreadyExists ? "Invoice already existed for booking." : "Invoice created from booking.", "Invoice", invoice.Id.ToString(), company.CompanyId, cancellationToken);
        if(!alreadyExists)await notifications.QueueAsync(new(invoice.Customer?.Email??invoice.CustomerId?.ToString()??"finance",invoice.CustomerId.HasValue?NotificationRecipientType.Customer:NotificationRecipientType.CorporateAccount,NotificationType.InvoiceGenerated,"Invoice generated",$"Invoice {invoice.InvoiceNumber} has been generated.","invoice-generated",CorrelationId:invoice.Id.ToString()),cancellationToken);
        return Results.Json(ToInvoiceDetail(invoice), statusCode: alreadyExists ? StatusCodes.Status200OK : StatusCodes.Status201Created);
    }

    private static InvoiceDetailDto ToInvoiceDetail(Invoice invoice) => new()
    {
        Id = invoice.Id, InvoiceNumber = invoice.InvoiceNumber, InvoiceDate = invoice.InvoiceDate, DueDate = invoice.DueDate,
        Status = invoice.Status.ToString(), CorporateAccountId = invoice.CorporateAccountId, CorporateAccountName = invoice.CorporateAccount?.AccountName,
        CustomerId = invoice.CustomerId, CustomerName = invoice.Customer is null ? null : $"{invoice.Customer.FirstName} {invoice.Customer.LastName}",
        Subtotal = invoice.Subtotal, TaxAmount = invoice.TaxAmount, TotalAmount = invoice.TotalAmount, AmountPaid = invoice.AmountPaid,
        BalanceDue = invoice.BalanceDue, Notes = invoice.Notes, CreatedAt = invoice.CreatedAt, UpdatedAt = invoice.UpdatedAt,
        Lines = invoice.Lines.Select(line => new InvoiceLineDto { Id = line.Id, BookingId = line.BookingId, Description = line.Description, Quantity = line.Quantity, UnitPrice = line.UnitPrice, TaxRate = line.TaxRate, LineSubtotal = line.LineSubtotal, TaxAmount = line.TaxAmount, LineTotal = line.LineTotal }).ToList()
    };

    private static async Task<IResult> GetBookingsAsync(LondonVIPDbContext db, ICompanyContext company, CancellationToken cancellationToken)
    {
        var bookings = await db.Bookings.AsNoTracking()
            .Where(booking => booking.CompanyId == company.CompanyId)
            .Select(booking => new BookingListItemDto
            {
                Id = booking.Id,
                BookingReference = booking.BookingReference,
                PickupDateTime = booking.PickupDateTime,
                CustomerName = booking.Customer.FirstName + " " + booking.Customer.LastName,
                PickupAddress = booking.PickupAddress,
                Destination = booking.Destination,
                AirportCode = booking.Airport == null ? null : booking.Airport.Code,
                FlightNumber = booking.FlightNumber,
                VehicleType = booking.VehicleType,
                TotalFare = booking.TotalFare,
                Status = booking.Status,
                PaymentStatus = booking.PaymentStatus,
                DriverName = booking.Driver == null ? null : booking.Driver.FirstName + " " + booking.Driver.LastName
                ,InvoiceNumber = db.InvoiceLines.Where(line => line.BookingId == booking.Id).Select(line => line.Invoice.InvoiceNumber).FirstOrDefault()
                ,InvoiceStatus = db.InvoiceLines.Where(line => line.BookingId == booking.Id).Select(line => line.Invoice.Status.ToString()).FirstOrDefault()
            }).ToListAsync(cancellationToken);

        bookings = bookings.OrderBy(booking => booking.PickupDateTime).ToList();

        return Results.Ok(bookings);
    }

    private static async Task<IResult> GetBookingAsync(Guid id, LondonVIPDbContext db, ICompanyContext company, CancellationToken cancellationToken)
    {
        var booking = await BookingQuery(db, company.CompanyId).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (booking is null) return Results.NotFound();
        var detail = ToDetail(booking);
        var invoice = await db.InvoiceLines.AsNoTracking().Where(line => line.BookingId == id && line.Invoice.CompanyId == company.CompanyId).Select(line => new { line.Invoice.Id, line.Invoice.InvoiceNumber, line.Invoice.Status }).FirstOrDefaultAsync(cancellationToken);
        if (invoice is not null) { detail.InvoiceId = invoice.Id; detail.InvoiceNumber = invoice.InvoiceNumber; detail.InvoiceStatus = invoice.Status.ToString(); }
        return Results.Ok(detail);
    }

    private static async Task<IResult> CreateBookingAsync(BookingCreateDto request, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, INotificationService notifications, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var errors = BookingValidator.Validate(request, now);
        await AddReferenceErrorsAsync(errors, request, db, company.CompanyId, cancellationToken);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            CompanyId = company.CompanyId,
            CreatedAt = now,
            UpdatedAt = now
        };
        booking.BookingReference = BookingReferenceGenerator.Generate(booking.Id, now);
        Apply(request, booking);
        db.Bookings.Add(booking);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("BookingCreated", "Booking", "Succeeded", SecurityEventSeverity.Information, "Booking created.", "Booking", booking.Id.ToString(), company.CompanyId, cancellationToken);
        if (booking.CorporateAccountId.HasValue) await audit.WriteAsync("CorporateAccountAssignedToBooking", "CorporateAccounts", "Succeeded", SecurityEventSeverity.Information, "Corporate account linked to booking.", "Booking", booking.Id.ToString(), company.CompanyId, cancellationToken);
        var recipient=await db.Customers.Where(x=>x.Id==booking.CustomerId&&x.CompanyId==company.CompanyId).Select(x=>x.Email).SingleAsync(cancellationToken);await notifications.QueueAsync(new(recipient,NotificationRecipientType.Customer,NotificationType.BookingCreated,"Booking received",$"Booking {booking.BookingReference} has been created.","booking-created",CorrelationId:booking.Id.ToString()),cancellationToken);

        var created = await BookingQuery(db, company.CompanyId).SingleAsync(item => item.Id == booking.Id, cancellationToken);
        return Results.Created($"/api/bookings/{booking.Id}", ToDetail(created));
    }

    private static async Task<IResult> UpdateBookingAsync(Guid id, BookingUpdateDto request, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, CancellationToken cancellationToken)
    {
        var errors = BookingValidator.Validate(request, DateTimeOffset.UtcNow);
        await AddReferenceErrorsAsync(errors, request, db, company.CompanyId, cancellationToken);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var booking = await db.Bookings.SingleOrDefaultAsync(item => item.Id == id && item.CompanyId == company.CompanyId, cancellationToken);
        if (booking is null) return Results.NotFound();

        var previousCorporateAccountId = booking.CorporateAccountId;
        Apply(request, booking);
        booking.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("BookingUpdated", "Booking", "Succeeded", SecurityEventSeverity.Information, "Booking operational details updated.", "Booking", id.ToString(), company.CompanyId, cancellationToken);
        if (previousCorporateAccountId != booking.CorporateAccountId) await audit.WriteAsync(booking.CorporateAccountId.HasValue ? "CorporateAccountAssignedToBooking" : "CorporateAccountRemovedFromBooking", "CorporateAccounts", "Succeeded", SecurityEventSeverity.Information, booking.CorporateAccountId.HasValue ? "Corporate account linked to booking." : "Corporate account removed from booking.", "Booking", id.ToString(), company.CompanyId, cancellationToken);

        var updated = await BookingQuery(db, company.CompanyId).SingleAsync(item => item.Id == id, cancellationToken);
        return Results.Ok(ToDetail(updated));
    }

    private static async Task<IResult> UpdateStatusAsync(Guid id, BookingStatusUpdateDto request, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, INotificationService notifications, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Status))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Booking status is invalid."] });

        var booking = await db.Bookings.SingleOrDefaultAsync(item => item.Id == id && item.CompanyId == company.CompanyId, cancellationToken);
        if (booking is null) return Results.NotFound();

        booking.Status = request.Status;
        booking.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("BookingStatusChanged", "Booking", "Succeeded", SecurityEventSeverity.Information, $"Booking status changed to {booking.Status}.", "Booking", id.ToString(), company.CompanyId, cancellationToken);
        var type=booking.Status switch{BookingStatus.Confirmed=>NotificationType.BookingConfirmed,BookingStatus.Cancelled=>NotificationType.BookingCancelled,BookingStatus.DriverEnRoute=>NotificationType.DriverEnRoute,BookingStatus.DriverArrived=>NotificationType.DriverArrived,BookingStatus.PassengerOnBoard=>NotificationType.PassengerOnboard,BookingStatus.Completed=>NotificationType.BookingCompleted,BookingStatus.NoShow=>NotificationType.NoShow,BookingStatus.UnableToComplete=>NotificationType.UnableToComplete,_=>(NotificationType?)null};if(type.HasValue){var recipient=await db.Customers.Where(x=>x.Id==booking.CustomerId).Select(x=>x.Email).SingleAsync(cancellationToken);await notifications.QueueAsync(new(recipient,NotificationRecipientType.Customer,type.Value,$"Booking {booking.Status}",$"Booking {booking.BookingReference} is now {booking.Status}.",$"booking-{booking.Status.ToString().ToLowerInvariant()}",CorrelationId:id.ToString()),cancellationToken);}
        return Results.Ok(new BookingStatusUpdateDto { Status = booking.Status });
    }

    private static async Task<IResult> GetLookupsAsync(LondonVIPDbContext db, ICompanyContext company, CancellationToken cancellationToken)
    {
        var result = new BookingLookupsDto
        {
            Customers = await db.Customers.AsNoTracking().Where(item => item.CompanyId == company.CompanyId && item.IsActive)
                .OrderBy(item => item.LastName).ThenBy(item => item.FirstName)
                .Select(item => new BookingLookupItemDto(item.Id, item.FirstName + " " + item.LastName)).ToListAsync(cancellationToken),
            Drivers = await db.Drivers.AsNoTracking().Where(item => item.CompanyId == company.CompanyId && item.IsActive)
                .OrderBy(item => item.LastName).ThenBy(item => item.FirstName)
                .Select(item => new BookingLookupItemDto(item.Id, item.FirstName + " " + item.LastName)).ToListAsync(cancellationToken),
            Airports = await db.Airports.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Name)
                .Select(item => new AirportLookupItemDto(item.Id, item.Code, item.Name)).ToListAsync(cancellationToken)
            ,CorporateAccounts = await db.CorporateAccounts.AsNoTracking().Where(item => item.CompanyId == company.CompanyId && item.IsActive && !item.IsOnHold)
                .OrderBy(item => item.AccountName).Select(item => new BookingLookupItemDto(item.Id, item.AccountNumber + " — " + item.AccountName)).ToListAsync(cancellationToken)
        };
        return Results.Ok(result);
    }

    private static IQueryable<Booking> BookingQuery(LondonVIPDbContext db, Guid companyId) =>
        db.Bookings.AsNoTracking()
            .Include(item => item.Customer).Include(item => item.Driver).Include(item => item.Airport).Include(item => item.CorporateAccount)
            .Where(item => item.CompanyId == companyId);

    private static BookingDetailDto ToDetail(Booking booking) => new()
    {
        Id = booking.Id,
        BookingReference = booking.BookingReference,
        CustomerId = booking.CustomerId,
        CustomerName = $"{booking.Customer.FirstName} {booking.Customer.LastName}".Trim(),
        CorporateAccountId = booking.CorporateAccountId,
        CorporateAccountName = booking.CorporateAccount?.AccountName,
        PurchaseOrderReference = booking.PurchaseOrderReference,
        BillingReference = booking.BillingReference,
        PickupAddress = booking.PickupAddress,
        Destination = booking.Destination,
        PickupDateTime = booking.PickupDateTime,
        PassengerCount = booking.PassengerCount,
        LuggageCount = booking.LuggageCount,
        VehicleType = booking.VehicleType,
        AirportId = booking.AirportId,
        AirportCode = booking.Airport?.Code,
        AirportName = booking.Airport?.Name,
        FlightNumber = booking.FlightNumber,
        IsAirportPickup = booking.IsAirportPickup,
        IsMeetAndGreet = booking.IsMeetAndGreet,
        CustomerNotes = booking.CustomerNotes,
        InternalNotes = booking.InternalNotes,
        BaseFare = booking.BaseFare,
        Extras = booking.Extras,
        TotalFare = booking.TotalFare,
        DriverId = booking.DriverId,
        DriverName = booking.Driver is null ? null : $"{booking.Driver.FirstName} {booking.Driver.LastName}".Trim(),
        Status = booking.Status,
        PaymentStatus = booking.PaymentStatus,
        CreatedAt = booking.CreatedAt,
        UpdatedAt = booking.UpdatedAt
    };

    private static void Apply(BookingCreateDto request, Booking booking)
    {
        booking.CustomerId = request.CustomerId;
        booking.CorporateAccountId = request.CorporateAccountId;
        booking.PurchaseOrderReference = NullIfWhiteSpace(request.PurchaseOrderReference);
        booking.BillingReference = NullIfWhiteSpace(request.BillingReference);
        booking.PickupAddress = request.PickupAddress.Trim();
        booking.Destination = request.Destination.Trim();
        booking.PickupDateTime = request.PickupDateTime;
        booking.PassengerCount = request.PassengerCount;
        booking.LuggageCount = request.LuggageCount;
        booking.VehicleType = request.VehicleType;
        booking.AirportId = request.AirportId;
        booking.FlightNumber = NullIfWhiteSpace(request.FlightNumber)?.ToUpperInvariant();
        booking.IsAirportPickup = request.IsAirportPickup;
        booking.IsMeetAndGreet = request.IsMeetAndGreet;
        booking.CustomerNotes = NullIfWhiteSpace(request.CustomerNotes);
        booking.InternalNotes = NullIfWhiteSpace(request.InternalNotes);
        booking.BaseFare = request.BaseFare;
        booking.Extras = request.Extras;
        booking.TotalFare = request.TotalFare;
        booking.DriverId = request.DriverId;
        booking.Status = request.Status;
        booking.PaymentStatus = request.PaymentStatus.Trim();
    }

    private static async Task AddReferenceErrorsAsync(Dictionary<string, string[]> errors, BookingCreateDto request, LondonVIPDbContext db, Guid companyId, CancellationToken cancellationToken)
    {
        if (request.CustomerId != Guid.Empty && !await db.Customers.AnyAsync(item => item.Id == request.CustomerId && item.CompanyId == companyId && item.IsActive, cancellationToken))
            errors["customerId"] = ["Customer was not found for the current company."];
        if (request.DriverId is { } driverId && !await db.Drivers.AnyAsync(item => item.Id == driverId && item.CompanyId == companyId && item.IsActive, cancellationToken))
            errors["driverId"] = ["Driver was not found for the current company."];
        if (request.AirportId is { } airportId && !await db.Airports.AnyAsync(item => item.Id == airportId && item.IsActive, cancellationToken))
            errors["airportId"] = ["Airport was not found."];
        if (request.PurchaseOrderReference?.Length > 100) errors["purchaseOrderReference"] = ["Purchase order reference must not exceed 100 characters."];
        if (request.BillingReference?.Length > 100) errors["billingReference"] = ["Billing reference must not exceed 100 characters."];
        if (request.CorporateAccountId is { } accountId)
        {
            var account = await db.CorporateAccounts.SingleOrDefaultAsync(item => item.Id == accountId && item.CompanyId == companyId, cancellationToken);
            if (account is null) errors["corporateAccountId"] = ["Corporate account was not found for the current company."];
            else if (!account.IsActive) errors["corporateAccountId"] = ["Corporate account is inactive."];
            else if (account.IsOnHold) errors["corporateAccountId"] = ["Corporate account is on hold and cannot be used for a new charge booking."];
            else if (account.PurchaseOrderRequired && string.IsNullOrWhiteSpace(request.PurchaseOrderReference)) errors["purchaseOrderReference"] = ["A purchase order reference is required for this corporate account."];
        }
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
