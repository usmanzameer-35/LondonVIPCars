using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.Maps;

public sealed record GeoCoordinate(double Latitude, double Longitude);
public sealed record GeocodingResult(bool Success, GeoCoordinate? Coordinate, string? FormattedAddress, string? Error);
public sealed record RouteRequest(GeoCoordinate Origin, GeoCoordinate Destination, IReadOnlyList<GeoCoordinate>? Stops = null, bool AvoidTolls = false, bool AvoidTraffic = false, string Preference = "fastest");
public sealed record RouteResult(bool Success, double DistanceMiles, int DurationMinutes, int? TrafficDurationMinutes, IReadOnlyList<GeoCoordinate> Path, string? Error = null);
public sealed record DriverLocationUpdateDto(Guid DriverId, Guid? BookingId, Guid? VehicleId, double Latitude, double Longitude, double? Heading, double? Speed, double? Accuracy, DateTimeOffset Timestamp);
public sealed record DriverLocationDto(Guid DriverId, string DriverName, Guid? VehicleId, string? Registration, Guid? BookingId, string? BookingReference, double Latitude, double Longitude, double? Heading, double? Speed, double? Accuracy, DateTimeOffset Timestamp, bool IsOnline);
public sealed record JourneySnapshotDto(Guid BookingId, double? DistanceMiles, int? DurationMinutes, DateTimeOffset? EstimatedArrival, string Status, DateTimeOffset CapturedAt);
public sealed record TrackingLinkDto(Guid BookingId, string Token, DateTimeOffset ExpiresAt, string RelativeUrl);
public sealed record CustomerTrackingDto(Guid BookingId, string BookingReference, BookingStatus Status, string? DriverName, string? Vehicle, string? Registration, DriverLocationDto? DriverLocation, DateTimeOffset? EstimatedArrival, int? RemainingMinutes);
public sealed record GeofenceDto(Guid Id, string Name, string Type, double Latitude, double Longitude, double RadiusMetres, bool IsActive);
public sealed record MapSearchResultDto(string Type, Guid Id, string Label, string? Subtitle, GeoCoordinate? Coordinate);
public sealed record LiveMapDto(IReadOnlyList<DriverLocationDto> Drivers, IReadOnlyList<GeofenceDto> Geofences, IReadOnlyList<MapSearchResultDto> Locations);

public interface IMapProvider
{
    Task<GeocodingResult> GeocodeAsync(string address, CancellationToken cancellationToken = default);
    Task<RouteResult> GetRouteAsync(RouteRequest request, CancellationToken cancellationToken = default);
}

public interface IGeocodingService { Task<GeocodingResult> GeocodeAsync(string address, CancellationToken cancellationToken = default); }
public interface IRouteService { Task<RouteResult> CalculateAsync(RouteRequest request, CancellationToken cancellationToken = default); }
public interface IGPSLocationService { Task<DriverLocationDto?> PublishAsync(DriverLocationUpdateDto update, CancellationToken cancellationToken = default); }
public interface ILiveTrackingService
{
    Task<IReadOnlyList<DriverLocationDto>> GetLiveDriversAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DriverLocationDto>> GetHistoryAsync(Guid bookingId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken = default);
    Task<TrackingLinkDto?> CreateTrackingLinkAsync(Guid bookingId, TimeSpan lifetime, CancellationToken cancellationToken = default);
    Task<CustomerTrackingDto?> GetCustomerTrackingAsync(string token, CancellationToken cancellationToken = default);
}
public interface IJourneyMonitoringService { Task<JourneySnapshotDto?> SnapshotAsync(Guid bookingId, RouteRequest route, CancellationToken cancellationToken = default); }
public interface IGeofenceService { Task<IReadOnlyList<GeofenceDto>> GetAllAsync(CancellationToken cancellationToken = default); Task<IReadOnlyList<GeofenceDto>> FindContainingAsync(GeoCoordinate coordinate, CancellationToken cancellationToken = default); }
public interface IAirportMonitoringService { Task<IReadOnlyList<MapSearchResultDto>> GetAirportMarkersAsync(CancellationToken cancellationToken = default); }

public interface IJourneyRealtimeClient
{
    Task LocationUpdated(DriverLocationDto location);
    Task JourneyUpdated(JourneySnapshotDto journey);
}
