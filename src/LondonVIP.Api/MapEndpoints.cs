using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Security;
using LondonVIP.Shared.Maps;
using LondonVIP.Shared.Security;
using LondonVIP.Shared.Tenancy;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LondonVIP.Api;

public static class MapEndpoints
{
    public static IEndpointRouteBuilder MapMapEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/maps").RequireAuthorization(SecurityPolicies.DispatchOperations).RequireRateLimiting("operations");
        group.MapGet("", GetMapAsync);
        group.MapGet("/drivers", (ILiveTrackingService service, CancellationToken token) => service.GetLiveDriversAsync(token));
        group.MapGet("/journeys/{bookingId:guid}/history", GetHistoryAsync);
        group.MapPost("/locations", PublishLocationAsync).RequireAuthorization(SecurityPolicies.ErpAccess);
        group.MapPost("/routes", (RouteRequest request, IRouteService service, CancellationToken token) => service.CalculateAsync(request, token));
        group.MapPost("/journeys/{bookingId:guid}/snapshot", SnapshotAsync);
        group.MapPost("/journeys/{bookingId:guid}/tracking-link", CreateTrackingLinkAsync);
        group.MapGet("/geofences", (IGeofenceService service, CancellationToken token) => service.GetAllAsync(token));
        group.MapGet("/search", SearchAsync);
        endpoints.MapGet("/api/tracking/{token}", GetPublicTrackingAsync).AllowAnonymous().RequireRateLimiting("public-quotes");
        return endpoints;
    }

    private static async Task<IResult> GetMapAsync(ILiveTrackingService tracking, IGeofenceService fences, IAirportMonitoringService airports, IAuditService audit, ICompanyContext company, CancellationToken token)
    {
        var result = new LiveMapDto(await tracking.GetLiveDriversAsync(token), await fences.GetAllAsync(token), await airports.GetAirportMarkersAsync(token));
        await audit.WriteAsync("LiveMapViewed", "JourneyIntelligence", "Succeeded", SecurityEventSeverity.Information, "Live journey map viewed.", companyId: company.CompanyId, cancellationToken: token);
        return Results.Ok(result);
    }

    private static async Task<IResult> PublishLocationAsync(DriverLocationUpdateDto request, IGPSLocationService locations, IHubContext<JourneyHub, IJourneyRealtimeClient> hub, IAuditService audit, ICompanyContext company, CancellationToken token)
    {
        var result = await locations.PublishAsync(request, token);
        if (result is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["location"] = ["Location, driver, vehicle or assigned booking is invalid."] });
        await hub.Clients.Group($"company:{company.CompanyId}").LocationUpdated(result);
        await audit.WriteAsync("DriverLocationPublished", "JourneyIntelligence", "Succeeded", SecurityEventSeverity.Information, "Driver location published.", "Driver", request.DriverId.ToString(), company.CompanyId, token);
        return Results.Accepted(value: result);
    }

    private static async Task<IResult> GetHistoryAsync(Guid bookingId, DateTimeOffset? from, DateTimeOffset? to, LondonVIPDbContext db, ILiveTrackingService tracking, ICompanyContext company, CancellationToken token)
    {
        if (!await db.Bookings.AnyAsync(x => x.Id == bookingId && x.CompanyId == company.CompanyId, token)) return Results.NotFound();
        return Results.Ok(await tracking.GetHistoryAsync(bookingId, from, to, token));
    }

    private static async Task<IResult> SnapshotAsync(Guid bookingId, RouteRequest request, IJourneyMonitoringService monitoring, IHubContext<JourneyHub, IJourneyRealtimeClient> hub, ICompanyContext company, CancellationToken token)
    {
        var result = await monitoring.SnapshotAsync(bookingId, request, token);
        if (result is null) return Results.NotFound();
        await hub.Clients.Group($"company:{company.CompanyId}").JourneyUpdated(result);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateTrackingLinkAsync(Guid bookingId, ILiveTrackingService tracking, IAuditService audit, ICompanyContext company, CancellationToken token)
    {
        var result = await tracking.CreateTrackingLinkAsync(bookingId, TimeSpan.FromHours(24), token);
        if (result is null) return Results.NotFound();
        await audit.WriteAsync("CustomerTrackingLinkCreated", "JourneyIntelligence", "Succeeded", SecurityEventSeverity.Information, "Expiring customer tracking link created.", "Booking", bookingId.ToString(), company.CompanyId, token);
        return Results.Created(result.RelativeUrl, result);
    }

    private static async Task<IResult> GetPublicTrackingAsync(string token, ILiveTrackingService tracking, CancellationToken cancellationToken)
    {
        var result = await tracking.GetCustomerTrackingAsync(token, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> SearchAsync(string? q, LondonVIPDbContext db, ICompanyContext company, CancellationToken token)
    {
        q = q?.Trim();
        if (string.IsNullOrWhiteSpace(q)) return Results.Ok(Array.Empty<MapSearchResultDto>());
        var pattern = $"%{q}%";
        var drivers = await db.Drivers.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && (EF.Functions.Like(x.FirstName, pattern) || EF.Functions.Like(x.LastName, pattern) || EF.Functions.Like(x.DriverNumber!, pattern))).Take(15).Select(x => new MapSearchResultDto("Driver", x.Id, x.FirstName + " " + x.LastName, x.DriverNumber, null)).ToListAsync(token);
        var bookings = await db.Bookings.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && EF.Functions.Like(x.BookingReference, pattern)).Take(15).Select(x => new MapSearchResultDto("Booking", x.Id, x.BookingReference, x.PickupAddress + " to " + x.Destination, null)).ToListAsync(token);
        var vehicles = await db.Vehicles.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && EF.Functions.Like(x.RegistrationNumber, pattern)).Take(15).Select(x => new MapSearchResultDto("Vehicle", x.Id, x.RegistrationNumber, x.Make + " " + x.Model, null)).ToListAsync(token);
        return Results.Ok(drivers.Concat(bookings).Concat(vehicles).Take(30));
    }
}
