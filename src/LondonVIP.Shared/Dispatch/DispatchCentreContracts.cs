using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.Dispatch;

public sealed class DispatchQuery
{
    public string? Search { get; set; }
    public DateOnly? Date { get; set; }
    public BookingStatus? Status { get; set; }
    public Guid? DriverId { get; set; }
    public Guid? VehicleId { get; set; }
    public bool? AirportOnly { get; set; }
    public bool? CorporateOnly { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
public sealed class DispatchPageDto<T> { public List<T> Items { get; set; }=[]; public int Page { get; set; } public int PageSize { get; set; } public int Total { get; set; } }
public sealed class DispatchDashboardDto { public DispatchKpiDto Kpis { get; set; }=new(); public List<DispatchBoardItemDto> WaitingBookings { get; set; }=[]; public List<DispatchDriverDto> Drivers { get; set; }=[]; public List<DispatchTimelineItemDto> Timeline { get; set; }=[]; public List<DispatchAlertDto> Alerts { get; set; }=[]; }
public sealed class DispatchKpiDto { public int BookingsWaiting { get; set; } public int Assigned { get; set; } public int DriversAvailable { get; set; } public int DriversBusy { get; set; } public int DriversOffline { get; set; } public int AirportJobsToday { get; set; } public int CorporateJobsToday { get; set; } public int CompletedToday { get; set; } public int CancelledToday { get; set; } public decimal RevenueToday { get; set; } public decimal? AverageEtaMinutes { get; set; } public decimal? AverageResponseMinutes { get; set; } public int OutstandingAlerts { get; set; } }
public sealed class DispatchDriverDto { public Guid DriverId { get; set; } public string Name { get; set; }=""; public string Phone { get; set; }=""; public Guid? VehicleId { get; set; } public VehicleType? VehicleType { get; set; } public string? Vehicle { get; set; } public string? Registration { get; set; } public string CalculatedStatus { get; set; }="Offline"; public Guid? CurrentBookingId { get; set; } public string? CurrentBookingReference { get; set; } public int TodaysJobs { get; set; } public int? EtaMinutes { get; set; } }
public sealed record DispatchTimelineItemDto(Guid Id,DateTimeOffset Timestamp,string Action,string Description,string? UserId,string? ResourceIdentifier,string CorrelationId);
public sealed record DispatchAlertDto(string Type,string Severity,string Title,string Detail,Guid? ResourceId,DateTimeOffset? DueAt);
public sealed class DriverRecommendationDto { public Guid DriverId { get; set; } public string DriverName { get; set; }=""; public string? Vehicle { get; set; } public string? Registration { get; set; } public int Score { get; set; } public int TodaysJobs { get; set; } public int? EstimatedArrivalMinutes { get; set; } public decimal? DistanceMiles { get; set; } public bool VehicleSuitable { get; set; } public List<string> Reasons { get; set; }=[]; }
public sealed class DispatchValidationResult { public bool IsValid => Errors.Count==0; public Dictionary<string,string[]> Errors { get; set; }=[]; }
public sealed record DispatchSearchResultDto(string Type,Guid Id,string Reference,string Title,string Subtitle,string Url);
public sealed record DriverPositionDto(Guid DriverId,decimal Latitude,decimal Longitude,DateTimeOffset RecordedAt);
public sealed record BookingPositionDto(Guid BookingId,decimal? PickupLatitude,decimal? PickupLongitude,decimal? DestinationLatitude,decimal? DestinationLongitude);
public sealed record AirportPositionDto(Guid AirportId,string Code,decimal Latitude,decimal Longitude);
public sealed record DispatchRouteDto(Guid BookingId,IReadOnlyList<DispatchCoordinateDto> Points,decimal? DistanceMiles,int? DurationMinutes);
public sealed record DispatchCoordinateDto(decimal Latitude,decimal Longitude);
public sealed record DispatchPinDto(string Type,Guid Id,decimal Latitude,decimal Longitude,string Label);
public sealed record DispatchClusterDto(decimal Latitude,decimal Longitude,int Count,IReadOnlyList<DispatchPinDto> Pins);
public interface IDispatchRealtimeClient { Task DispatchChangedAsync(Guid companyId,string eventType,Guid? bookingId); }

public interface IDispatchService { Task<DispatchPageDto<DispatchBoardItemDto>> GetBookingsAsync(DispatchQuery query,CancellationToken token=default); Task<DispatchBoardItemDto?> GetBookingAsync(Guid id,CancellationToken token=default); Task<List<DispatchSearchResultDto>> SearchAsync(string term,int limit,CancellationToken token=default); }
public interface IDriverAvailabilityService { Task<List<DispatchDriverDto>> GetDriversAsync(CancellationToken token=default); }
public interface IAssignmentEngine { Task<DispatchValidationResult> ValidateAsync(Guid bookingId,Guid driverId,CancellationToken token=default); }
public interface IConflictDetectionService { Task<bool> HasConflictAsync(Guid bookingId,Guid driverId,CancellationToken token=default); }
public interface IDispatchTimelineService { Task<List<DispatchTimelineItemDto>> GetAsync(Guid? bookingId,int limit,CancellationToken token=default); }
public interface IDriverRecommendationService { Task<List<DriverRecommendationDto>> RecommendAsync(Guid bookingId,CancellationToken token=default); }
public interface IDispatchDashboardService { Task<DispatchDashboardDto> GetAsync(CancellationToken token=default); Task<List<DispatchAlertDto>> GetAlertsAsync(CancellationToken token=default); }
