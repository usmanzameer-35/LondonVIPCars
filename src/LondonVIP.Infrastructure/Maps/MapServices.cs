using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Shared.Maps;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LondonVIP.Infrastructure.Maps;

public sealed class GoogleMapsProvider : IMapProvider
{
    public Task<GeocodingResult> GeocodeAsync(string address, CancellationToken cancellationToken = default) =>
        Task.FromResult(new GeocodingResult(false, null, null, "Google Maps is not configured."));

    public Task<RouteResult> GetRouteAsync(RouteRequest request, CancellationToken cancellationToken = default)
    {
        var points = new[] { request.Origin }.Concat(request.Stops ?? []).Append(request.Destination).ToArray();
        var miles = points.Zip(points.Skip(1), (a, b) => Spatial.DistanceMiles(a, b)).Sum();
        var minutes = (int)Math.Ceiling(miles / 25d * 60d);
        return Task.FromResult(new RouteResult(true, Math.Round(miles, 2), minutes, null, points));
    }
}

public sealed class GeocodingService(IMapProvider provider) : IGeocodingService
{
    public Task<GeocodingResult> GeocodeAsync(string address, CancellationToken cancellationToken = default) => provider.GeocodeAsync(address, cancellationToken);
}

public sealed class RouteService(IMapProvider provider) : IRouteService
{
    public Task<RouteResult> CalculateAsync(RouteRequest request, CancellationToken cancellationToken = default) => provider.GetRouteAsync(request, cancellationToken);
}

public sealed class GPSLocationService(LondonVIPDbContext db, ICompanyContext company, TimeProvider clock) : IGPSLocationService
{
    public async Task<DriverLocationDto?> PublishAsync(DriverLocationUpdateDto update, CancellationToken cancellationToken = default)
    {
        if (!Spatial.IsValid(update.Latitude, update.Longitude) || update.Accuracy is < 0 || update.Speed is < 0 || update.Heading is < 0 or >= 360) return null;
        var driver = await db.Drivers.AsNoTracking().Where(x => x.Id == update.DriverId && x.CompanyId == company.CompanyId && x.IsActive)
            .Select(x => new { x.Id, Name = x.FirstName + " " + x.LastName, x.VehicleId }).SingleOrDefaultAsync(cancellationToken);
        if (driver is null) return null;
        if (update.VehicleId is { } vehicleId && !await db.Vehicles.AnyAsync(x => x.Id == vehicleId && x.CompanyId == company.CompanyId && x.IsActive, cancellationToken)) return null;
        if (update.BookingId is { } bookingId && !await db.Bookings.AnyAsync(x => x.Id == bookingId && x.CompanyId == company.CompanyId && x.DriverId == driver.Id, cancellationToken)) return null;
        var recordedAt = update.Timestamp == default ? clock.GetUtcNow() : update.Timestamp;
        if (recordedAt > clock.GetUtcNow().AddMinutes(5)) return null;
        var row = new DriverLocation { Id = Guid.NewGuid(), CompanyId = company.CompanyId, DriverId = driver.Id, VehicleId = update.VehicleId ?? driver.VehicleId, BookingId = update.BookingId, Latitude = update.Latitude, Longitude = update.Longitude, Heading = update.Heading, Speed = update.Speed, Accuracy = update.Accuracy, RecordedAt = recordedAt };
        db.DriverLocations.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        var details = await db.DriverLocations.AsNoTracking().Where(x => x.Id == row.Id).Select(LocationProjection).SingleAsync(cancellationToken);
        return details with { IsOnline = true };
    }

    internal static readonly System.Linq.Expressions.Expression<Func<DriverLocation, DriverLocationDto>> LocationProjection = x =>
        new(x.DriverId, x.Driver.FirstName + " " + x.Driver.LastName, x.VehicleId, x.Vehicle == null ? null : x.Vehicle.RegistrationNumber, x.BookingId, x.Booking == null ? null : x.Booking.BookingReference, x.Latitude, x.Longitude, x.Heading, x.Speed, x.Accuracy, x.RecordedAt, false);
}

public sealed class LiveTrackingService(LondonVIPDbContext db, ICompanyContext company, TimeProvider clock) : ILiveTrackingService
{
    public async Task<IReadOnlyList<DriverLocationDto>> GetLiveDriversAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = clock.GetUtcNow().AddMinutes(-5);
        var latest = db.DriverLocations.AsNoTracking().Where(x => x.CompanyId == company.CompanyId &&
            x.RecordedAt == db.DriverLocations.Where(y => y.CompanyId == company.CompanyId && y.DriverId == x.DriverId).Max(y => y.RecordedAt));
        var rows = await latest.Select(GPSLocationService.LocationProjection).ToListAsync(cancellationToken);
        return rows.Select(x => x with { IsOnline = x.Timestamp >= cutoff }).OrderByDescending(x => x.IsOnline).ThenBy(x => x.DriverName).ToList();
    }

    public async Task<IReadOnlyList<DriverLocationDto>> GetHistoryAsync(Guid bookingId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken = default)
    {
        if (!await db.Bookings.AnyAsync(x => x.Id == bookingId && x.CompanyId == company.CompanyId, cancellationToken)) return [];
        var query = db.DriverLocations.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.BookingId == bookingId);
        if (from is not null) query = query.Where(x => x.RecordedAt >= from);
        if (to is not null) query = query.Where(x => x.RecordedAt <= to);
        return await query.OrderBy(x => x.RecordedAt).Take(5000).Select(GPSLocationService.LocationProjection).ToListAsync(cancellationToken);
    }

    public async Task<TrackingLinkDto?> CreateTrackingLinkAsync(Guid bookingId, TimeSpan lifetime, CancellationToken cancellationToken = default)
    {
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromDays(7) || !await db.Bookings.AnyAsync(x => x.Id == bookingId && x.CompanyId == company.CompanyId, cancellationToken)) return null;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expires = clock.GetUtcNow().Add(lifetime);
        db.CustomerTrackingTokens.Add(new CustomerTrackingToken { Id = Guid.NewGuid(), CompanyId = company.CompanyId, BookingId = bookingId, TokenHash = Hash(token), CreatedAt = clock.GetUtcNow(), ExpiresAt = expires });
        await db.SaveChangesAsync(cancellationToken);
        return new TrackingLinkDto(bookingId, token, expires, $"/track/{token}");
    }

    public async Task<CustomerTrackingDto?> GetCustomerTrackingAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var hash = Hash(token);
        var row = await db.CustomerTrackingTokens.Include(x => x.Booking).ThenInclude(x => x.Driver).ThenInclude(x => x!.Vehicle)
            .SingleOrDefaultAsync(x => x.TokenHash == hash && !x.IsRevoked && x.ExpiresAt > clock.GetUtcNow(), cancellationToken);
        if (row is null) return null;
        row.UsedAt ??= clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        var booking = row.Booking;
        var location = await db.DriverLocations.AsNoTracking().Where(x => x.CompanyId == row.CompanyId && x.BookingId == booking.Id)
            .OrderByDescending(x => x.RecordedAt).Select(GPSLocationService.LocationProjection).FirstOrDefaultAsync(cancellationToken);
        var snapshot = await db.JourneySnapshots.AsNoTracking().Where(x => x.CompanyId == row.CompanyId && x.BookingId == booking.Id).OrderByDescending(x => x.CapturedAt).FirstOrDefaultAsync(cancellationToken);
        return new CustomerTrackingDto(booking.Id, booking.BookingReference, booking.Status, booking.Driver == null ? null : booking.Driver.FirstName + " " + booking.Driver.LastName, booking.Driver?.Vehicle is null ? null : booking.Driver.Vehicle.Make + " " + booking.Driver.Vehicle.Model, booking.Driver?.Vehicle?.RegistrationNumber, location, snapshot?.EstimatedArrival, snapshot?.DurationMinutes);
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public sealed class JourneyMonitoringService(LondonVIPDbContext db, ICompanyContext company, IRouteService routes, TimeProvider clock) : IJourneyMonitoringService
{
    public async Task<JourneySnapshotDto?> SnapshotAsync(Guid bookingId, RouteRequest route, CancellationToken cancellationToken = default)
    {
        var booking = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == bookingId && x.CompanyId == company.CompanyId, cancellationToken);
        if (booking is null) return null;
        var result = await routes.CalculateAsync(route, cancellationToken);
        if (!result.Success) return null;
        var now = clock.GetUtcNow();
        var eta = now.AddMinutes(result.TrafficDurationMinutes ?? result.DurationMinutes);
        var snapshot = new JourneySnapshot { Id = Guid.NewGuid(), CompanyId = company.CompanyId, BookingId = bookingId, DistanceMiles = result.DistanceMiles, DurationMinutes = result.TrafficDurationMinutes ?? result.DurationMinutes, EstimatedArrival = eta, RouteJson = JsonSerializer.Serialize(result.Path), Status = booking.Status.ToString(), CapturedAt = now };
        db.JourneySnapshots.Add(snapshot);
        await db.SaveChangesAsync(cancellationToken);
        return new JourneySnapshotDto(bookingId, snapshot.DistanceMiles, snapshot.DurationMinutes, eta, snapshot.Status, now);
    }
}

public sealed class GeofenceService(LondonVIPDbContext db, ICompanyContext company) : IGeofenceService
{
    public async Task<IReadOnlyList<GeofenceDto>> GetAllAsync(CancellationToken cancellationToken = default) => await db.Geofences.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.IsActive).OrderBy(x => x.Name).Select(x => new GeofenceDto(x.Id, x.Name, x.Type, x.Latitude, x.Longitude, x.RadiusMetres, x.IsActive)).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<GeofenceDto>> FindContainingAsync(GeoCoordinate coordinate, CancellationToken cancellationToken = default) => (await GetAllAsync(cancellationToken)).Where(x => Spatial.DistanceMetres(coordinate, new(x.Latitude, x.Longitude)) <= x.RadiusMetres).ToList();
}

public sealed class AirportMonitoringService(LondonVIPDbContext db) : IAirportMonitoringService
{
    public async Task<IReadOnlyList<MapSearchResultDto>> GetAirportMarkersAsync(CancellationToken cancellationToken = default) => await db.Airports.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new MapSearchResultDto("Airport", x.Id, x.Name, x.Code, null)).ToListAsync(cancellationToken);
}

internal static class Spatial
{
    public static bool IsValid(double latitude, double longitude) => latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
    public static double DistanceMiles(GeoCoordinate a, GeoCoordinate b) => DistanceMetres(a, b) / 1609.344d;
    public static double DistanceMetres(GeoCoordinate a, GeoCoordinate b)
    {
        const double radius = 6371000d;
        var lat = Degrees(b.Latitude - a.Latitude); var lon = Degrees(b.Longitude - a.Longitude);
        var value = Math.Sin(lat / 2) * Math.Sin(lat / 2) + Math.Cos(Degrees(a.Latitude)) * Math.Cos(Degrees(b.Latitude)) * Math.Sin(lon / 2) * Math.Sin(lon / 2);
        return radius * 2 * Math.Atan2(Math.Sqrt(value), Math.Sqrt(1 - value));
    }
    private static double Degrees(double value) => value * Math.PI / 180d;
}
