using LondonVIP.Shared.Models;
using LondonVIP.Shared.Pricing;

namespace LondonVIP.Infrastructure.Pricing;

public static class PricingCalculator
{
    public const string PricingNotConfiguredMessage = "Pricing is not configured for the selected airport and vehicle type.";

    public static QuoteResponse Calculate(QuoteRequest request, PricingRule? rule)
    {
        if (rule is null || !rule.IsActive)
        {
            return new QuoteResponse
            {
                IsConfigured = false,
                Message = PricingNotConfiguredMessage
            };
        }

        var chargeableWaitingMinutes = Math.Max(0, request.WaitingMinutes - rule.FreeWaitingMinutes);
        var waitingCharge = Math.Round(
            rule.WaitingChargePerHour * chargeableWaitingMinutes / 60m,
            2,
            MidpointRounding.AwayFromZero);
        var airportPickupSupplement = request.IsAirportPickup
            ? rule.AirportPickupSupplement
            : 0m;
        var extrasTotal = airportPickupSupplement + waitingCharge;

        return new QuoteResponse
        {
            IsConfigured = true,
            BaseFare = rule.BasePrice,
            AirportPickupSupplement = airportPickupSupplement,
            FreeWaitingMinutes = rule.FreeWaitingMinutes,
            ChargeableWaitingMinutes = chargeableWaitingMinutes,
            WaitingChargePerHour = rule.WaitingChargePerHour,
            WaitingCharge = waitingCharge,
            ExtrasTotal = extrasTotal,
            TotalFare = rule.BasePrice + extrasTotal
        };
    }
}
