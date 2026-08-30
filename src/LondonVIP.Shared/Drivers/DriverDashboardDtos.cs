using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.Drivers;

public sealed class DriverDashboardDto
{
    public Guid DriverId { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public DriverAvailabilityStatus AvailabilityStatus { get; set; }
    public string? VehicleDisplay { get; set; }
    public string? RegistrationNumber { get; set; }
    public DateTimeOffset? NextPickupTime { get; set; }
    public DriverDashboardJobDto? CurrentJob { get; set; }
    public List<DriverDashboardJobDto> UpcomingJobs { get; set; } = [];
    public List<DriverDashboardJobDto> TodaysJobs { get; set; } = [];
    public int CompletedToday { get; set; }
    public int CancelledToday { get; set; }
    public int RejectedToday { get; set; }
}

public sealed class DriverDashboardJobDto
{
    public Guid BookingId { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public DateTimeOffset PickupDateTime { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string PickupAddress { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string? AirportCode { get; set; }
    public string? FlightNumber { get; set; }
    public BookingStatus Status { get; set; }
}
