using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.Customers;

public sealed class CustomerBookingHistoryItemDto
{
    public Guid BookingId { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public DateTimeOffset PickupDateTime { get; set; }
    public string PickupAddress { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public VehicleType VehicleType { get; set; }
    public BookingStatus Status { get; set; }
    public decimal TotalFare { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string? DriverName { get; set; }
}
