using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.Bookings;

public sealed class BookingDetailDto
{
    public Guid Id { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Guid? CorporateAccountId { get; set; }
    public string? CorporateAccountName { get; set; }
    public string? PurchaseOrderReference { get; set; }
    public string? BillingReference { get; set; }
    public string PickupAddress { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTimeOffset PickupDateTime { get; set; }
    public int PassengerCount { get; set; }
    public int LuggageCount { get; set; }
    public VehicleType VehicleType { get; set; }
    public Guid? AirportId { get; set; }
    public string? AirportCode { get; set; }
    public string? AirportName { get; set; }
    public string? FlightNumber { get; set; }
    public bool IsAirportPickup { get; set; }
    public bool IsMeetAndGreet { get; set; }
    public string? CustomerNotes { get; set; }
    public string? InternalNotes { get; set; }
    public decimal BaseFare { get; set; }
    public decimal Extras { get; set; }
    public decimal TotalFare { get; set; }
    public Guid? DriverId { get; set; }
    public string? DriverName { get; set; }
    public BookingStatus Status { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? InvoiceStatus { get; set; }
}
