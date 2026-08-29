using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.Bookings;

public class BookingCreateDto
{
    public Guid CustomerId { get; set; }
    public Guid? CorporateAccountId { get; set; }
    public string? PurchaseOrderReference { get; set; }
    public string? BillingReference { get; set; }
    public string PickupAddress { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTimeOffset PickupDateTime { get; set; }
    public int PassengerCount { get; set; } = 1;
    public int LuggageCount { get; set; }
    public VehicleType VehicleType { get; set; }
    public Guid? AirportId { get; set; }
    public string? FlightNumber { get; set; }
    public bool IsAirportPickup { get; set; }
    public bool IsMeetAndGreet { get; set; }
    public string? CustomerNotes { get; set; }
    public string? InternalNotes { get; set; }
    public decimal BaseFare { get; set; }
    public decimal Extras { get; set; }
    public decimal TotalFare { get; set; }
    public Guid? DriverId { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public string PaymentStatus { get; set; } = "Pending";
}
