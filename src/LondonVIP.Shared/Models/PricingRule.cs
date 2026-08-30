namespace LondonVIP.Shared.Models;

public class PricingRule
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public Guid? AirportId { get; set; }

    public VehicleType VehicleType { get; set; }

    public decimal BasePrice { get; set; }

    public decimal AirportPickupSupplement { get; set; }

    public int FreeWaitingMinutes { get; set; }

    public decimal WaitingChargePerHour { get; set; }

    public PricingRuleType RuleType { get; set; } = PricingRuleType.LegacyFare;

    public string Name { get; set; } = string.Empty;

    public int Priority { get; set; }

    public DateTimeOffset? EffectiveFrom { get; set; }

    public DateTimeOffset? EffectiveTo { get; set; }

    public string? PickupPostcode { get; set; }

    public string? DestinationPostcode { get; set; }

    public string? PickupZone { get; set; }

    public string? DestinationZone { get; set; }

    public string? PromotionCode { get; set; }

    public decimal Amount { get; set; }

    public decimal Percentage { get; set; }

    public decimal UnitRate { get; set; }

    public decimal IncludedUnits { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public enum PricingRuleType
{
    LegacyFare = 0,
    AirportFixedFare = 1,
    PostcodeFixedFare = 2,
    ZoneFixedFare = 3,
    HourlyHire = 4,
    Distance = 5,
    WaitingTime = 6,
    ParkingCharge = 7,
    MeetAndGreet = 8,
    ChildSeat = 9,
    ExtraLuggage = 10,
    ExtraStop = 11,
    NightSurcharge = 12,
    WeekendSurcharge = 13,
    HolidaySurcharge = 14,
    CorporateDiscount = 15,
    PromotionalDiscount = 16,
    MinimumFare = 17,
    CancellationFee = 18,
    TollCharge = 19,
    ManualAdjustment = 20
}
