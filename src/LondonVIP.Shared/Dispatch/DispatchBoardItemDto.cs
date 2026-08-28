using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.Dispatch;

public sealed class DispatchBoardItemDto
{
    public Guid BookingId { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public DateTimeOffset PickupDateTime { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string PickupAddress { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string? AirportCode { get; set; }
    public string? FlightNumber { get; set; }
    public VehicleType VehicleType { get; set; }
    public Guid? DriverId { get; set; }
    public string? DriverName { get; set; }
    public string? DriverVehicleRegistration { get; set; }
    public decimal TotalFare { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public BookingStatus Status { get; set; }
    public DispatchTimingState TimingState { get; set; }
}
