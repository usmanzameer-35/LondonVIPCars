using LondonVIP.Shared.Maps;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Notifications;

namespace LondonVIP.Shared.Drivers;

public sealed record DriverPortalProfileDto(Guid DriverId, string Name, string Email, string Phone, string? DriverNumber, DriverAvailabilityStatus Status, string ComplianceStatus, DriverPortalVehicleDto? Vehicle);
public sealed record DriverPortalVehicleDto(Guid Id, string Registration, string Make, string Model, VehicleType VehicleType, string? Colour, DateOnly? MotExpiry, DateOnly? InsuranceExpiry, DateOnly? LicenceExpiry, string ComplianceStatus);
public sealed record DriverPortalJobDto(Guid Id, string BookingReference, DateTimeOffset PickupDateTime, string PickupAddress, string Destination, string PassengerName, string PassengerPhone, VehicleType VehicleType, int PassengerCount, int LuggageCount, string? FlightNumber, bool MeetAndGreet, string? CorporateAccount, string PaymentStatus, string? DriverNotes, BookingStatus Status);
public sealed record DriverShiftDto(Guid Id, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, DateTimeOffset? BreakStartedAt, int BreakMinutes, int JobsCompleted, bool IsActive);
public sealed record DriverPortalDashboardDto(DriverPortalProfileDto Profile, DriverShiftDto? CurrentShift, DriverPortalJobDto? CurrentJob, DriverPortalJobDto? NextJob, int JobsToday, int CompletedToday, decimal? EarningsToday, int OutstandingAlerts, int DocumentWarnings, DateTimeOffset? LatestLocationAt);
public sealed record DriverEarningsDto(decimal GrossToday, decimal GrossWeek, decimal? CommissionToday, decimal? CommissionWeek, bool CommissionConfigured, int CompletedJobsToday, int CompletedJobsWeek);
public sealed record DriverDocumentDto(string Type, string? Reference, DateOnly? ExpiryDate, string Status, int? DaysRemaining);
public sealed record DriverNotificationDto(Guid Id, string Subject, string Body, NotificationType Type, NotificationStatus Status, DateTimeOffset CreatedAt);
public sealed record DriverCommandResult(bool Success, string Code, string Message, BookingStatus? Status = null);
public sealed record DriverAvailabilityResult(bool Success, DriverAvailabilityStatus Status, IReadOnlyList<string> Reasons);
public sealed record DriverDeclineRequest(string Reason, string? Note);
public sealed record DriverExceptionRequest(bool Confirmed, string Reason, string? Note);
public sealed record VehicleIssueRequest(string Category, string Severity, string Description, Guid? BookingId);

public interface IDriverPortalService { Task<DriverPortalProfileDto?> GetProfileAsync(CancellationToken token = default); Task<DriverPortalDashboardDto?> GetDashboardAsync(CancellationToken token = default); }
public interface IDriverJobService { Task<IReadOnlyList<DriverPortalJobDto>> GetJobsAsync(CancellationToken token = default); Task<DriverPortalJobDto?> GetJobAsync(Guid bookingId, CancellationToken token = default); Task<DriverCommandResult> AcceptAsync(Guid bookingId, CancellationToken token = default); Task<DriverCommandResult> DeclineAsync(Guid bookingId, DriverDeclineRequest request, CancellationToken token = default); Task<DriverCommandResult> TransitionAsync(Guid bookingId, BookingStatus next, DriverExceptionRequest? details = null, CancellationToken token = default); }
public interface IDriverShiftService { Task<DriverShiftDto?> GetCurrentAsync(CancellationToken token = default); Task<DriverCommandResult> StartAsync(CancellationToken token = default); Task<DriverCommandResult> EndAsync(CancellationToken token = default); Task<DriverCommandResult> StartBreakAsync(CancellationToken token = default); Task<DriverCommandResult> EndBreakAsync(CancellationToken token = default); Task<DriverAvailabilityResult> SetOnlineAsync(bool online, CancellationToken token = default); }
public interface IDriverEarningsService { Task<DriverEarningsDto?> GetAsync(CancellationToken token = default); }
public interface IDriverDocumentService { Task<IReadOnlyList<DriverDocumentDto>?> GetAsync(CancellationToken token = default); }
