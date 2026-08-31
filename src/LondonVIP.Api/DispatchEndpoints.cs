using LondonVIP.Infrastructure.Data;
using LondonVIP.Shared.Dispatch;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;
using LondonVIP.Infrastructure.Security;
using LondonVIP.Shared.Security;
using LondonVIP.Infrastructure.Bookings;
using LondonVIP.Shared.Notifications;
using LondonVIP.Infrastructure.Dispatch;

namespace LondonVIP.Api;

public static class DispatchEndpoints
{
    private static readonly BookingStatus[] OperationalStatuses =
        [BookingStatus.Confirmed, BookingStatus.Assigned, BookingStatus.DriverEnRoute, BookingStatus.DriverArrived, BookingStatus.PassengerOnBoard];

    public static IEndpointRouteBuilder MapDispatchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/dispatch").RequireAuthorization(SecurityPolicies.DispatchOperations).RequireRateLimiting("operations");
        group.MapGet("", GetBoardAsync);
        group.MapGet("/unassigned", GetUnassignedAsync);
        group.MapGet("/drivers", GetDriversAsync);
        group.MapGet("/dashboard", GetCentreDashboardAsync);
        group.MapGet("/bookings", GetCentreBookingsAsync);
        group.MapGet("/bookings/{id:guid}", GetCentreBookingAsync);
        group.MapGet("/bookings/{id:guid}/recommendations", GetRecommendationsAsync);
        group.MapGet("/timeline", GetTimelineAsync);
        group.MapGet("/alerts", GetAlertsAsync);
        group.MapGet("/search", SearchAsync);
        group.MapPost("/bookings/{bookingId:guid}/assign", CentreAssignAsync);
        group.MapPost("/bookings/{bookingId:guid}/reassign", CentreAssignAsync);
        group.MapPost("/bookings/{bookingId:guid}/unassign", CentreUnassignAsync);
        group.MapPost("/bookings/{bookingId:guid}/accept", AcceptAsync);
        group.MapPost("/bookings/{bookingId:guid}/decline", RejectAsync);
        group.MapPost("/bookings/{bookingId:guid}/start", StartNavigationAsync);
        group.MapPost("/bookings/{bookingId:guid}/arrived", ArriveAsync);
        group.MapPost("/bookings/{bookingId:guid}/onboard", PassengerOnboardAsync);
        group.MapPost("/bookings/{bookingId:guid}/complete", CompleteAsync);
        group.MapPost("/bookings/{bookingId:guid}/cancel", CancelAsync);
        group.MapPost("/bookings/{bookingId:guid}/noshow", NoShowAsync);
        group.MapPost("/bookings/{bookingId:guid}/unable", UnableToCompleteAsync);
        group.MapPatch("/{bookingId:guid}/assign", AssignDriverAsync);
        group.MapPatch("/{bookingId:guid}/unassign", UnassignDriverAsync);
        group.MapPatch("/{bookingId:guid}/status", UpdateStatusAsync);
        // Command-style aliases used by operational clients.
        endpoints.MapPost("/api/bookings/{bookingId:guid}/assign", AssignDriverAsync).RequireAuthorization(SecurityPolicies.DispatchOperations).RequireRateLimiting("operations");
        endpoints.MapPost("/api/bookings/{bookingId:guid}/unassign", UnassignDriverAsync).RequireAuthorization(SecurityPolicies.DispatchOperations).RequireRateLimiting("operations");
        endpoints.MapPost("/api/bookings/{bookingId:guid}/accept", AcceptAsync).RequireAuthorization(SecurityPolicies.DispatchOperations).RequireRateLimiting("operations");
        endpoints.MapPost("/api/bookings/{bookingId:guid}/reject", RejectAsync).RequireAuthorization(SecurityPolicies.DispatchOperations).RequireRateLimiting("operations");
        endpoints.MapPost("/api/bookings/{bookingId:guid}/start-navigation", StartNavigationAsync).RequireAuthorization(SecurityPolicies.DispatchOperations).RequireRateLimiting("operations");
        endpoints.MapPost("/api/bookings/{bookingId:guid}/arrive", ArriveAsync).RequireAuthorization(SecurityPolicies.DispatchOperations).RequireRateLimiting("operations");
        endpoints.MapPost("/api/bookings/{bookingId:guid}/passenger-onboard", PassengerOnboardAsync).RequireAuthorization(SecurityPolicies.DispatchOperations).RequireRateLimiting("operations");
        endpoints.MapPost("/api/bookings/{bookingId:guid}/complete", CompleteAsync).RequireAuthorization(SecurityPolicies.DispatchOperations).RequireRateLimiting("operations");
        endpoints.MapPost("/api/bookings/{bookingId:guid}/no-show", NoShowAsync).RequireAuthorization(SecurityPolicies.DispatchOperations).RequireRateLimiting("operations");
        endpoints.MapPost("/api/bookings/{bookingId:guid}/unable-to-complete", UnableToCompleteAsync).RequireAuthorization(SecurityPolicies.DispatchOperations).RequireRateLimiting("operations");
        return endpoints;
    }

    private static async Task<IResult> GetCentreDashboardAsync(IDispatchDashboardService service,IAuditService audit,ICompanyContext company,CancellationToken token){await AuditViewAsync(audit,company,"Dashboard",token);return Results.Ok(await service.GetAsync(token));}
    private static async Task<IResult> GetCentreBookingsAsync([AsParameters] DispatchQuery query,IDispatchService service,IAuditService audit,ICompanyContext company,CancellationToken token){await AuditViewAsync(audit,company,"Bookings",token);return Results.Ok(await service.GetBookingsAsync(query,token));}
    private static async Task<IResult> GetCentreBookingAsync(Guid id,IDispatchService service,IAuditService audit,ICompanyContext company,CancellationToken token){var result=await service.GetBookingAsync(id,token);if(result is null)return Results.NotFound();await AuditViewAsync(audit,company,"BookingDetail",token);return Results.Ok(result);}
    private static async Task<IResult> GetRecommendationsAsync(Guid id,IDriverRecommendationService service,IAuditService audit,ICompanyContext company,CancellationToken token){await AuditViewAsync(audit,company,"DriverRecommendations",token);return Results.Ok(await service.RecommendAsync(id,token));}
    private static async Task<IResult> GetTimelineAsync(Guid? bookingId,int? limit,IDispatchTimelineService service,IAuditService audit,ICompanyContext company,CancellationToken token){await AuditViewAsync(audit,company,"Timeline",token);return Results.Ok(await service.GetAsync(bookingId,limit??50,token));}
    private static async Task<IResult> GetAlertsAsync(IDispatchDashboardService service,IAuditService audit,ICompanyContext company,CancellationToken token){await AuditViewAsync(audit,company,"Alerts",token);return Results.Ok(await service.GetAlertsAsync(token));}
    private static async Task<IResult> SearchAsync(string? q,int? limit,IDispatchService service,IAuditService audit,ICompanyContext company,CancellationToken token){if(string.IsNullOrWhiteSpace(q))return Results.Ok(Array.Empty<DispatchSearchResultDto>());await AuditViewAsync(audit,company,"Search",token);return Results.Ok(await service.SearchAsync(q,limit??20,token));}
    private static async Task<IResult> CentreAssignAsync(Guid bookingId,AssignDriverRequest request,IAssignmentEngine engine,LondonVIPDbContext db,ICompanyContext company,IAuditService audit,INotificationService notifications,BookingTransitionService transitions,CancellationToken token){var validation=await engine.ValidateAsync(bookingId,request.DriverId,token);if(!validation.IsValid)return Results.ValidationProblem(validation.Errors);return await AssignDriverAsync(bookingId,request,db,company,audit,notifications,transitions,token);}
    private static Task<IResult> CentreUnassignAsync(Guid bookingId,LondonVIPDbContext db,ICompanyContext company,IAuditService audit,INotificationService notifications,CancellationToken token)=>UnassignDriverAsync(bookingId,null,db,company,audit,notifications,token);
    private static Task AuditViewAsync(IAuditService audit,ICompanyContext company,string resource,CancellationToken token)=>audit.WriteAsync("DispatchViewed","Dispatch","Succeeded",SecurityEventSeverity.Information,$"Dispatch {resource} viewed.","DispatchCentre",resource,company.CompanyId,token);

    private static Task<IResult> StartNavigationAsync(Guid bookingId, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, BookingTransitionService transitions,INotificationService n,CancellationToken ct) => ApplyTransitionAsync(bookingId, BookingStatus.DriverEnRoute, "DriverNavigationStarted", db, company, audit, transitions,n,ct);
    private static Task<IResult> ArriveAsync(Guid bookingId, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, BookingTransitionService transitions,INotificationService n,CancellationToken ct) => ApplyTransitionAsync(bookingId, BookingStatus.DriverArrived, "DriverArrived", db, company, audit, transitions,n,ct);
    private static Task<IResult> PassengerOnboardAsync(Guid bookingId, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, BookingTransitionService transitions,INotificationService n,CancellationToken ct) => ApplyTransitionAsync(bookingId, BookingStatus.PassengerOnBoard, "PassengerOnBoard", db, company, audit, transitions,n,ct);
    private static Task<IResult> CompleteAsync(Guid bookingId, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, BookingTransitionService transitions,INotificationService n,CancellationToken ct) => ApplyTransitionAsync(bookingId, BookingStatus.Completed, "BookingCompleted", db, company, audit, transitions,n,ct);
    private static Task<IResult> NoShowAsync(Guid bookingId, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, BookingTransitionService transitions,INotificationService n,CancellationToken ct) => ApplyTransitionAsync(bookingId, BookingStatus.NoShow, "BookingMarkedNoShow", db, company, audit, transitions,n,ct);
    private static Task<IResult> UnableToCompleteAsync(Guid bookingId, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, BookingTransitionService transitions,INotificationService n,CancellationToken ct) => ApplyTransitionAsync(bookingId, BookingStatus.UnableToComplete, "BookingUnableToComplete", db, company, audit, transitions,n,ct);
    private static Task<IResult> CancelAsync(Guid bookingId, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, BookingTransitionService transitions,INotificationService n,CancellationToken ct) => ApplyTransitionAsync(bookingId, BookingStatus.Cancelled, "BookingCancelled", db, company, audit, transitions,n,ct);

    private static async Task<IResult> ApplyTransitionAsync(Guid bookingId, BookingStatus next, string eventType, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, BookingTransitionService transitions,INotificationService notifications,CancellationToken ct)
    {
        var booking = await db.Bookings.SingleOrDefaultAsync(x => x.Id == bookingId && x.CompanyId == company.CompanyId, ct);
        if (booking is null) { await AuditCrossTenantAsync(db, audit, bookingId, company.CompanyId, ct); return Results.NotFound(); }
        if (!transitions.CanTransition(booking.Status, next, booking.DriverId.HasValue)) return Conflict($"Status cannot move from {booking.Status} to {next} in dispatch.");
        booking.Status = next; booking.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(eventType, "Dispatch", "Succeeded", SecurityEventSeverity.Information, $"Booking moved to {next}.", "Booking", bookingId.ToString(), company.CompanyId, ct);
        var type=next switch{BookingStatus.DriverEnRoute=>NotificationType.DriverEnRoute,BookingStatus.DriverArrived=>NotificationType.DriverArrived,BookingStatus.PassengerOnBoard=>NotificationType.PassengerOnboard,BookingStatus.Completed=>NotificationType.BookingCompleted,BookingStatus.NoShow=>NotificationType.NoShow,_=>NotificationType.UnableToComplete};var recipient=await db.Customers.Where(x=>x.Id==booking.CustomerId).Select(x=>x.Email).SingleAsync(ct);await notifications.QueueAsync(new(recipient,NotificationRecipientType.Customer,type,$"Journey {next}",$"Booking {booking.BookingReference} is now {next}.",$"dispatch-{next.ToString().ToLowerInvariant()}",CorrelationId:bookingId.ToString()),ct);
        return OperationalStatuses.Contains(next) ? Results.Ok(await LoadItemAsync(db, bookingId, company.CompanyId, ct)) : Results.Ok(new DispatchStatusUpdateDto { Status = next });
    }

    private static async Task<IResult> AcceptAsync(Guid bookingId, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.SingleOrDefaultAsync(x => x.Id == bookingId && x.CompanyId == company.CompanyId, cancellationToken);
        if (booking is null) return Results.NotFound();
        if (booking.Status != BookingStatus.Assigned || booking.DriverId is null) return Conflict("Only assigned bookings can be accepted.");
        await audit.WriteAsync("DriverAccepted", "Dispatch", "Succeeded", SecurityEventSeverity.Information, "Driver accepted booking.", "Booking", bookingId.ToString(), company.CompanyId, cancellationToken);
        return Results.Ok(await LoadItemAsync(db, bookingId, company.CompanyId, cancellationToken));
    }

    private static async Task<IResult> RejectAsync(Guid bookingId, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.SingleOrDefaultAsync(x => x.Id == bookingId && x.CompanyId == company.CompanyId, cancellationToken);
        if (booking is null) return Results.NotFound();
        if (booking.Status != BookingStatus.Assigned || booking.DriverId is null) return Conflict("Only assigned bookings can be rejected.");
        var rejectedByDriverId = booking.DriverId.Value;
        booking.DriverId = null; booking.Status = BookingStatus.Confirmed; booking.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("DriverRejected", "Dispatch", "Succeeded", SecurityEventSeverity.Information, $"Driver {rejectedByDriverId} rejected booking.", "Booking", bookingId.ToString(), company.CompanyId, cancellationToken);
        return Results.Ok(await LoadItemAsync(db, bookingId, company.CompanyId, cancellationToken));
    }

    private static async Task<IResult> GetBoardAsync(LondonVIPDbContext db, ICompanyContext company, CancellationToken cancellationToken) =>
        Results.Ok(await LoadBoardAsync(db, company.CompanyId, false, cancellationToken));

    private static async Task<IResult> GetUnassignedAsync(LondonVIPDbContext db, ICompanyContext company, CancellationToken cancellationToken) =>
        Results.Ok(await LoadBoardAsync(db, company.CompanyId, true, cancellationToken));

    private static async Task<IResult> GetDriversAsync(LondonVIPDbContext db, ICompanyContext company, CancellationToken cancellationToken)
    {
        var drivers = await db.Drivers.AsNoTracking()
            .Where(driver => driver.CompanyId == company.CompanyId && driver.IsActive)
            .OrderBy(driver => driver.LastName).ThenBy(driver => driver.FirstName)
            .Select(driver => new DriverAvailabilityDto
            {
                DriverId = driver.Id,
                DriverName = driver.FirstName + " " + driver.LastName,
                Phone = driver.Phone,
                VehicleId = driver.VehicleId,
                VehicleDisplay = driver.Vehicle == null ? null : driver.Vehicle.Make + " " + driver.Vehicle.Model,
                RegistrationNumber = driver.Vehicle == null ? null : driver.Vehicle.RegistrationNumber,
                VehicleType = driver.Vehicle == null ? null : driver.Vehicle.VehicleType,
                IsActive = driver.IsActive
                ,AvailabilityStatus = driver.AvailabilityStatus
            }).ToListAsync(cancellationToken);

        return Results.Ok(drivers);
    }

    private static async Task<IResult> AssignDriverAsync(
        Guid bookingId,
        AssignDriverRequest request,
        LondonVIPDbContext db,
        ICompanyContext company,
        IAuditService audit,
        INotificationService notifications,
        BookingTransitionService transitions,
        CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.SingleOrDefaultAsync(item => item.Id == bookingId && item.CompanyId == company.CompanyId, cancellationToken);
        if (booking is null) { await AuditCrossTenantAsync(db, audit, bookingId, company.CompanyId, cancellationToken); return Results.NotFound(); }
        if (!transitions.CanAssign(booking.Status))
            return Conflict("Only confirmed or assigned bookings can be assigned or reassigned.");
        if (request.DriverId == Guid.Empty)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["driverId"] = ["Driver is required."] });

        var driver = await db.Drivers.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.DriverId && item.CompanyId == company.CompanyId, cancellationToken);
        if (driver is null) { await AuditCrossTenantAsync(db, audit, request.DriverId, company.CompanyId, cancellationToken, "Driver"); return Results.NotFound(); }
        if (!driver.IsActive) return Conflict("The selected driver is inactive.");

        if (driver.VehicleId is { } vehicleId)
        {
            var validVehicle = await db.Vehicles.AnyAsync(vehicle => vehicle.Id == vehicleId && vehicle.CompanyId == company.CompanyId && vehicle.IsActive, cancellationToken);
            if (!validVehicle) return Conflict("The driver's vehicle is unavailable or does not belong to the current company.");
        }

        var wasAssigned = booking.DriverId.HasValue;
        booking.DriverId = driver.Id;
        booking.Status = BookingStatus.Assigned;
        booking.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(wasAssigned ? "DriverReassigned" : "DriverAssigned", "Dispatch", "Succeeded", SecurityEventSeverity.Information, "Driver assigned to booking.", "Booking", booking.Id.ToString(), company.CompanyId, cancellationToken);
        await notifications.QueueAsync(new(driver.Phone,NotificationRecipientType.Driver,NotificationType.DriverAssigned,"New journey assigned",$"Booking {booking.BookingReference} has been assigned to you.","driver-assigned",NotificationChannel.InternalErp,booking.Id.ToString()),cancellationToken);
        return Results.Ok(await LoadItemAsync(db, booking.Id, company.CompanyId, cancellationToken));
    }

    private static async Task<IResult> UnassignDriverAsync(
        Guid bookingId,
        UnassignDriverRequest? request,
        LondonVIPDbContext db,
        ICompanyContext company,
        IAuditService audit,
        INotificationService notifications,
        CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.SingleOrDefaultAsync(item => item.Id == bookingId && item.CompanyId == company.CompanyId, cancellationToken);
        if (booking is null) { await AuditCrossTenantAsync(db, audit, bookingId, company.CompanyId, cancellationToken); return Results.NotFound(); }
        if (booking.Status != BookingStatus.Assigned || booking.DriverId is null)
            return Conflict("Only an assigned booking can be unassigned before the journey starts.");

        booking.DriverId = null;
        booking.Status = BookingStatus.Confirmed;
        booking.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("DriverUnassigned", "Dispatch", "Succeeded", SecurityEventSeverity.Information, "Driver unassigned from booking.", "Booking", booking.Id.ToString(), company.CompanyId, cancellationToken);
        await notifications.QueueAsync(new(booking.CustomerId.ToString(),NotificationRecipientType.Customer,NotificationType.DriverUnassigned,"Driver assignment updated",$"The driver assignment for {booking.BookingReference} was removed.","driver-unassigned",CorrelationId:booking.Id.ToString()),cancellationToken);
        return Results.Ok(await LoadItemAsync(db, booking.Id, company.CompanyId, cancellationToken));
    }

    private static async Task<IResult> UpdateStatusAsync(
        Guid bookingId,
        DispatchStatusUpdateDto request,
        LondonVIPDbContext db,
        ICompanyContext company,
        IAuditService audit,
        BookingTransitionService transitions,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Status)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Dispatch status is invalid."] });
        var booking = await db.Bookings.SingleOrDefaultAsync(item => item.Id == bookingId && item.CompanyId == company.CompanyId, cancellationToken);
        if (booking is null) { await AuditCrossTenantAsync(db, audit, bookingId, company.CompanyId, cancellationToken); return Results.NotFound(); }
        if (!transitions.CanTransition(booking.Status, request.Status, booking.DriverId is not null))
            return Conflict($"Status cannot move from {booking.Status} to {request.Status} in dispatch.");

        booking.Status = request.Status;
        booking.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("DispatchStatusChanged", "Dispatch", "Succeeded", SecurityEventSeverity.Information, $"Dispatch status changed to {booking.Status}.", "Booking", booking.Id.ToString(), company.CompanyId, cancellationToken);

        if (OperationalStatuses.Contains(booking.Status))
            return Results.Ok(await LoadItemAsync(db, booking.Id, company.CompanyId, cancellationToken));

        return Results.Ok(new DispatchStatusUpdateDto { Status = booking.Status });
    }

    private static async Task<List<DispatchBoardItemDto>> LoadBoardAsync(
        LondonVIPDbContext db,
        Guid companyId,
        bool unassignedOnly,
        CancellationToken cancellationToken)
    {
        var query = db.Bookings.AsNoTracking()
            .Where(booking => booking.CompanyId == companyId && OperationalStatuses.Contains(booking.Status));
        if (unassignedOnly)
            query = query.Where(booking => booking.Status == BookingStatus.Confirmed && booking.DriverId == null);

        var items = await Project(query).ToListAsync(cancellationToken);
        SetTimingStates(items, DateTimeOffset.UtcNow);
        return items.OrderBy(item => item.PickupDateTime).ToList();
    }

    private static async Task<DispatchBoardItemDto?> LoadItemAsync(LondonVIPDbContext db, Guid bookingId, Guid companyId, CancellationToken cancellationToken)
    {
        var item = await Project(db.Bookings.AsNoTracking().Where(booking => booking.Id == bookingId && booking.CompanyId == companyId))
            .SingleOrDefaultAsync(cancellationToken);
        if (item is not null) SetTimingStates([item], DateTimeOffset.UtcNow);
        return item;
    }

    private static IQueryable<DispatchBoardItemDto> Project(IQueryable<Booking> query) =>
        query.Select(booking => new DispatchBoardItemDto
        {
            BookingId = booking.Id,
            BookingReference = booking.BookingReference,
            PickupDateTime = booking.PickupDateTime,
            CustomerName = booking.Customer.FirstName + " " + booking.Customer.LastName,
            PickupAddress = booking.PickupAddress,
            Destination = booking.Destination,
            AirportCode = booking.Airport == null ? null : booking.Airport.Code,
            FlightNumber = booking.FlightNumber,
            VehicleType = booking.VehicleType,
            DriverId = booking.DriverId,
            DriverName = booking.Driver == null ? null : booking.Driver.FirstName + " " + booking.Driver.LastName,
            DriverVehicleRegistration = booking.Driver == null || booking.Driver.Vehicle == null ? null : booking.Driver.Vehicle.RegistrationNumber,
            TotalFare = booking.TotalFare,
            PaymentStatus = booking.PaymentStatus,
            Status = booking.Status
        });

    private static void SetTimingStates(IEnumerable<DispatchBoardItemDto> items, DateTimeOffset now)
    {
        foreach (var item in items)
        {
            item.TimingState = item.Status is BookingStatus.DriverEnRoute or BookingStatus.PassengerOnBoard
                ? DispatchTimingState.Active
                : item.PickupDateTime < now
                    ? DispatchTimingState.Overdue
                    : item.PickupDateTime <= now.AddHours(1)
                        ? DispatchTimingState.Approaching
                        : DispatchTimingState.Scheduled;
        }
    }

    private static IResult Conflict(string message) => Results.Conflict(new { message });

    private static async Task AuditCrossTenantAsync(LondonVIPDbContext db, IAuditService audit, Guid id, Guid companyId, CancellationToken cancellationToken, string resource = "Booking")
    {
        var existsElsewhere = resource == "Driver"
            ? await db.Drivers.AnyAsync(item => item.Id == id && item.CompanyId != companyId, cancellationToken)
            : await db.Bookings.AnyAsync(item => item.Id == id && item.CompanyId != companyId, cancellationToken);
        if (existsElsewhere) await audit.WriteAsync("CrossTenantAccessAttempt", "Authorization", "Denied", SecurityEventSeverity.High, $"Cross-tenant {resource.ToLowerInvariant()} access was blocked.", resource, id.ToString(), companyId, cancellationToken);
    }
}
