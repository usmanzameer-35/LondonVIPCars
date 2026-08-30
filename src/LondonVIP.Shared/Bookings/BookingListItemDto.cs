using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.Bookings;

public sealed class BookingListItemDto
{
    public Guid Id { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public DateTimeOffset PickupDateTime { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string PickupAddress { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string? AirportCode { get; set; }
    public string? FlightNumber { get; set; }
    public VehicleType VehicleType { get; set; }
    public decimal TotalFare { get; set; }
    public BookingStatus Status { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string? DriverName { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? InvoiceStatus { get; set; }
}
