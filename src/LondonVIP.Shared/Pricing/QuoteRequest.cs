using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.Pricing;

public class QuoteRequest
{
    public string PickupAddress { get; set; } = string.Empty;

    public string Destination { get; set; } = string.Empty;

    public Guid? AirportId { get; set; }

    public VehicleType VehicleType { get; set; }

    public int PassengerCount { get; set; }

    public int LuggageCount { get; set; }

    public bool IsAirportPickup { get; set; }

    public int WaitingMinutes { get; set; }

    public bool IsMeetAndGreet { get; set; }

    public DateTimeOffset? JourneyDateTime { get; set; }
    public string? PickupPostcode { get; set; }
    public string? DestinationPostcode { get; set; }
    public string? PickupZone { get; set; }
    public string? DestinationZone { get; set; }
    public decimal HireHours { get; set; }
    public decimal DistanceMiles { get; set; }
    public decimal ParkingCharges { get; set; }
    public int ChildSeatCount { get; set; }
    public int ExtraStopCount { get; set; }
    public decimal TollCharges { get; set; }
    public decimal ManualAdjustment { get; set; }
    public bool IsCorporateCustomer { get; set; }
    public string? PromotionCode { get; set; }
    public bool IsHoliday { get; set; }
    public bool IsCancellation { get; set; }
}
