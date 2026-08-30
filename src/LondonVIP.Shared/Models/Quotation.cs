namespace LondonVIP.Shared.Models;

public class Quotation
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid? CorporateAccountId { get; set; }
    public CorporateAccount? CorporateAccount { get; set; }
    public Guid? ConvertedBookingId { get; set; }
    public Booking? ConvertedBooking { get; set; }
    public string QuoteReference { get; set; } = string.Empty;
    public QuoteStatus Status { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? ConvertedAt { get; set; }
    public string PickupAddress { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTimeOffset PickupDateTime { get; set; }
    public int PassengerCount { get; set; }
    public int LuggageCount { get; set; }
    public VehicleType VehicleType { get; set; }
    public Guid? AirportId { get; set; }
    public Airport? Airport { get; set; }
    public string? FlightNumber { get; set; }
    public bool IsAirportPickup { get; set; }
    public bool IsMeetAndGreet { get; set; }
    public string? Notes { get; set; }
    public decimal BaseFare { get; set; }
    public decimal Extras { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TotalFare { get; set; }
    public string PricingBreakdownJson { get; set; } = "[]";
    public string PricingRequestJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public enum QuoteStatus { Draft = 0, Active = 1, Expired = 2, Converted = 3, Cancelled = 4 }
