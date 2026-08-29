using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.Pricing;

public class PricingRuleCreateDto
{
    public Guid? AirportId { get; set; }
    public VehicleType VehicleType { get; set; }
    public decimal BasePrice { get; set; }
    public decimal AirportPickupSupplement { get; set; }
    public int FreeWaitingMinutes { get; set; }
    public decimal WaitingChargePerHour { get; set; }
    public bool IsActive { get; set; } = true;
}
