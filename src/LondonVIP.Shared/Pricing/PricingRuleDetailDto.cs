using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.Pricing;

public sealed class PricingRuleDetailDto
{
    public Guid Id { get; set; }
    public Guid? AirportId { get; set; }
    public string? AirportCode { get; set; }
    public string? AirportName { get; set; }
    public VehicleType VehicleType { get; set; }
    public decimal BasePrice { get; set; }
    public decimal AirportPickupSupplement { get; set; }
    public int FreeWaitingMinutes { get; set; }
    public decimal WaitingChargePerHour { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public PricingRuleType RuleType { get; set; }
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
}
