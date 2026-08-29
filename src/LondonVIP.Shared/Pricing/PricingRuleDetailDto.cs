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
}
