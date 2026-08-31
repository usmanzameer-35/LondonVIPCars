using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Drivers;
using LondonVIP.Infrastructure.Security;
using LondonVIP.Shared.Drivers;
using LondonVIP.Shared.Maps;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Security;
using LondonVIP.Shared.Tenancy;
using LondonVIP.Shared.Workflows;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LondonVIP.Api;

public static class DriverPortalEndpoints
{
    public static IEndpointRouteBuilder MapDriverPortalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/driver").RequireAuthorization(SecurityPolicies.DriverPortal).RequireRateLimiting("operations");
        group.MapGet("/me", Profile); group.MapGet("/dashboard", Dashboard); group.MapGet("/jobs", Jobs); group.MapGet("/jobs/{id:guid}", Job);
        group.MapPost("/jobs/{id:guid}/accept", Accept); group.MapPost("/jobs/{id:guid}/decline", Decline);
        group.MapPost("/jobs/{id:guid}/enroute", (Guid id, IDriverJobService jobs, CancellationToken token) => Command(jobs.TransitionAsync(id, BookingStatus.DriverEnRoute, token: token)));
        group.MapPost("/jobs/{id:guid}/arrived", (Guid id, IDriverJobService jobs, CancellationToken token) => Command(jobs.TransitionAsync(id, BookingStatus.DriverArrived, token: token)));
        group.MapPost("/jobs/{id:guid}/onboard", (Guid id, IDriverJobService jobs, CancellationToken token) => Command(jobs.TransitionAsync(id, BookingStatus.PassengerOnBoard, token: token)));
        group.MapPost("/jobs/{id:guid}/complete", (Guid id, IDriverJobService jobs, CancellationToken token) => Command(jobs.TransitionAsync(id, BookingStatus.Completed, token: token)));
        group.MapPost("/jobs/{id:guid}/noshow", (Guid id, DriverExceptionRequest request, IDriverJobService jobs, CancellationToken token) => Command(jobs.TransitionAsync(id, BookingStatus.NoShow, request, token)));
        group.MapPost("/jobs/{id:guid}/unable", (Guid id, DriverExceptionRequest request, IDriverJobService jobs, CancellationToken token) => Command(jobs.TransitionAsync(id, BookingStatus.UnableToComplete, request, token)));
        group.MapPost("/status/online", (IDriverShiftService shifts, CancellationToken token) => Availability(shifts.SetOnlineAsync(true, token)));
        group.MapPost("/status/offline", (IDriverShiftService shifts, CancellationToken token) => Availability(shifts.SetOnlineAsync(false, token)));
        group.MapPost("/status/break/start", (IDriverShiftService shifts, CancellationToken token) => Command(shifts.StartBreakAsync(token)));
        group.MapPost("/status/break/end", (IDriverShiftService shifts, CancellationToken token) => Command(shifts.EndBreakAsync(token)));
        group.MapPost("/shift/start", (IDriverShiftService shifts, CancellationToken token) => Command(shifts.StartAsync(token)));
        group.MapPost("/shift/end", (IDriverShiftService shifts, CancellationToken token) => Command(shifts.EndAsync(token)));
        group.MapPost("/location", Location);
        group.MapGet("/earnings", Earnings); group.MapGet("/documents", Documents); group.MapGet("/vehicle", Vehicle); group.MapGet("/notifications", Notifications);
        group.MapPost("/vehicle/issues", VehicleIssue);
        return endpoints;
    }

    private static async Task<IResult> Profile(IDriverPortalService service, CancellationToken token) => (await service.GetProfileAsync(token)) is { } result ? Results.Ok(result) : Results.NotFound();
    private static async Task<IResult> Dashboard(IDriverPortalService service, CancellationToken token) => (await service.GetDashboardAsync(token)) is { } result ? Results.Ok(result) : Results.NotFound();
    private static async Task<IResult> Jobs(IDriverJobService service, CancellationToken token) => Results.Ok(await service.GetJobsAsync(token));
    private static async Task<IResult> Job(Guid id, IDriverJobService service, CancellationToken token) => (await service.GetJobAsync(id, token)) is { } result ? Results.Ok(result) : Results.NotFound();
    private static Task<IResult> Accept(Guid id, IDriverJobService service, CancellationToken token) => Command(service.AcceptAsync(id, token));
    private static Task<IResult> Decline(Guid id, DriverDeclineRequest request, IDriverJobService service, CancellationToken token) => Command(service.DeclineAsync(id, request, token));
    private static async Task<IResult> Command(Task<DriverCommandResult> operation) { var result = await operation; return result.Success ? Results.Ok(result) : result.Code == "NotFound" ? Results.NotFound() : result.Code == "ValidationFailure" || result.Code == "ConfirmationRequired" ? Results.BadRequest(result) : Results.Conflict(result); }
    private static async Task<IResult> Availability(Task<DriverAvailabilityResult> operation) { var result = await operation; return result.Success ? Results.Ok(result) : Results.Conflict(result); }
    private static async Task<IResult> Earnings(IDriverEarningsService service, CancellationToken token) => (await service.GetAsync(token)) is { } result ? Results.Ok(result) : Results.NotFound();
    private static async Task<IResult> Documents(IDriverDocumentService service, CancellationToken token) => (await service.GetAsync(token)) is { } result ? Results.Ok(result) : Results.NotFound();
    private static async Task<IResult> Vehicle(IDriverPortalService service, CancellationToken token) => (await service.GetProfileAsync(token))?.Vehicle is { } result ? Results.Ok(result) : Results.NotFound();
    private static async Task<IResult> Notifications(IDriverPortalService portal, LondonVIPDbContext db, ICompanyContext company, CancellationToken token)
    {
        var profile = await portal.GetProfileAsync(token); if (profile is null) return Results.NotFound(); var recipients = new[] { profile.DriverId.ToString(), profile.Email, profile.Phone };
        return Results.Ok(await db.Notifications.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.RecipientType == NotificationRecipientType.Driver && recipients.Contains(x.Recipient)).OrderByDescending(x => x.CreatedAt).Take(100).Select(x => new DriverNotificationDto(x.Id, x.Subject, x.Body, x.NotificationType, x.Status, x.CreatedAt)).ToListAsync(token));
    }
    private static async Task<IResult> Location(DriverLocationUpdateDto request, IDriverIdentityResolver identity, IGPSLocationService gps, IHubContext<JourneyHub, IJourneyRealtimeClient> hub, ICompanyContext company, CancellationToken token)
    {
        var driverId = await identity.GetDriverIdAsync(token); if (driverId is null) return Results.NotFound(); var result = await gps.PublishAsync(request with { DriverId = driverId.Value }, token); if (result is null) return Results.BadRequest(new { message = "Location is invalid or not operationally permitted." }); await hub.Clients.Group($"company:{company.CompanyId}").LocationUpdated(result); if (result.BookingId is { } bookingId) await hub.Clients.Group($"booking:{bookingId}").LocationUpdated(result); return Results.Accepted(value: result);
    }
    private static async Task<IResult> VehicleIssue(VehicleIssueRequest request, IDriverIdentityResolver identity, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, IBusinessEventPublisher events, CancellationToken token)
    {
        var driverId = await identity.GetDriverIdAsync(token); if (driverId is null) return Results.NotFound(); var driver = await db.Drivers.SingleOrDefaultAsync(x => x.Id == driverId && x.CompanyId == company.CompanyId, token); if (driver?.VehicleId is null) return Results.Conflict(new { message = "No vehicle is assigned." });
        string[] categories = ["Breakdown", "Warning Light", "Tyre Issue", "Damage", "Accident", "Cleaning Required", "Mechanical Issue", "Other"]; string[] severities = ["Low", "Medium", "High", "Critical"];
        if (!categories.Contains(request.Category, StringComparer.OrdinalIgnoreCase) || !severities.Contains(request.Severity, StringComparer.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(request.Description) || request.Description.Length > 2000) return Results.ValidationProblem(new Dictionary<string, string[]> { ["issue"] = ["Category, severity and a description up to 2000 characters are required."] });
        if (request.BookingId is { } bookingId && !await db.Bookings.AnyAsync(x => x.Id == bookingId && x.CompanyId == company.CompanyId && x.DriverId == driverId, token)) return Results.NotFound();
        var issue = new DriverVehicleIssue { Id = Guid.NewGuid(), CompanyId = company.CompanyId, DriverId = driverId.Value, VehicleId = driver.VehicleId.Value, BookingId = request.BookingId, Category = request.Category, Severity = request.Severity, Description = request.Description.Trim(), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }; db.DriverVehicleIssues.Add(issue); await db.SaveChangesAsync(token); await audit.WriteAsync("VehicleIssueReported", "DriverOperations", "Succeeded", SecurityEventSeverity.Warning, request.Description, "DriverVehicleIssue", issue.Id.ToString(), company.CompanyId, token); await events.PublishAsync(new("VehicleIssueReported", "DriverVehicleIssue", issue.Id, "{}"), token); return Results.Created($"/api/driver/vehicle/issues/{issue.Id}", new { issue.Id, issue.Status });
    }
}
