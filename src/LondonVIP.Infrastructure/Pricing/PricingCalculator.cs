using LondonVIP.Shared.Models;
using LondonVIP.Shared.Pricing;

namespace LondonVIP.Infrastructure.Pricing;

public static class PricingCalculator
{
    public const string PricingNotConfiguredMessage = "Pricing is not configured for the selected airport and vehicle type.";

    public static QuoteResponse Calculate(QuoteRequest request, PricingRule? rule)
        => CalculateRules(request, rule is null ? [] : [rule]);

    public static QuoteResponse CalculateRules(QuoteRequest request, IReadOnlyCollection<PricingRule> rules)
    {
        var selected = rules.Where(rule => rule.IsActive)
            .GroupBy(rule => rule.RuleType)
            .Select(group => group.OrderByDescending(rule => rule.Priority).ThenByDescending(rule => rule.UpdatedAt).First())
            .ToList();
        var baseRule = selected.Where(rule => rule.RuleType is PricingRuleType.LegacyFare or PricingRuleType.AirportFixedFare or PricingRuleType.PostcodeFixedFare or PricingRuleType.ZoneFixedFare or PricingRuleType.HourlyHire or PricingRuleType.Distance)
            .OrderByDescending(rule => rule.Priority).ThenByDescending(rule => rule.UpdatedAt).FirstOrDefault();
        if (baseRule is null)
        {
            return new QuoteResponse
            {
                IsConfigured = false,
                Message = PricingNotConfiguredMessage
            };
        }

        var waitingRule = selected.FirstOrDefault(rule => rule.RuleType == PricingRuleType.WaitingTime) ?? baseRule;
        var chargeableWaitingMinutes = Math.Max(0, request.WaitingMinutes - waitingRule.FreeWaitingMinutes);
        var waitingCharge = Math.Round(
            Rate(waitingRule) * chargeableWaitingMinutes / 60m,
            2,
            MidpointRounding.AwayFromZero);
        var airportPickupSupplement = request.IsAirportPickup
            ? baseRule.AirportPickupSupplement
            : 0m;
        var baseFare = BaseFare(baseRule, request);
        var breakdown = new List<PricingBreakdownItemDto> { new(Name(baseRule), baseFare, baseRule.Id) };
        Add(breakdown, "Airport pickup", airportPickupSupplement, baseRule.Id);
        Add(breakdown, "Waiting time", waitingCharge, waitingRule.Id);

        foreach (var rule in selected.Where(rule => rule != baseRule && rule != waitingRule))
        {
            var amount = rule.RuleType switch
            {
                PricingRuleType.ParkingCharge => request.ParkingCharges > 0 ? request.ParkingCharges : rule.Amount,
                PricingRuleType.MeetAndGreet => request.IsMeetAndGreet ? rule.Amount : 0,
                PricingRuleType.ChildSeat => rule.UnitRate * request.ChildSeatCount,
                PricingRuleType.ExtraLuggage => rule.UnitRate * Math.Max(0, request.LuggageCount - (int)rule.IncludedUnits),
                PricingRuleType.ExtraStop => rule.UnitRate * request.ExtraStopCount,
                PricingRuleType.NightSurcharge => IsNight(request.JourneyDateTime) ? Surcharge(rule, baseFare) : 0,
                PricingRuleType.WeekendSurcharge => IsWeekend(request.JourneyDateTime) ? Surcharge(rule, baseFare) : 0,
                PricingRuleType.HolidaySurcharge => request.IsHoliday ? Surcharge(rule, baseFare) : 0,
                PricingRuleType.TollCharge => request.TollCharges > 0 ? request.TollCharges : rule.Amount,
                PricingRuleType.ManualAdjustment => request.ManualAdjustment,
                PricingRuleType.CancellationFee => request.IsCancellation ? rule.Amount : 0,
                _ => 0
            };
            Add(breakdown, Name(rule), amount, rule.Id);
        }

        var gross = breakdown.Sum(item => item.Amount);
        var discounts = selected.Where(rule => rule.RuleType is PricingRuleType.CorporateDiscount or PricingRuleType.PromotionalDiscount)
            .Sum(rule => rule.Amount > 0 ? rule.Amount : Math.Round(gross * rule.Percentage / 100m, 2, MidpointRounding.AwayFromZero));
        var minimum = selected.FirstOrDefault(rule => rule.RuleType == PricingRuleType.MinimumFare)?.Amount ?? 0;
        var total = Math.Max(minimum, gross - discounts);
        var extrasTotal = gross - baseFare;

        return new QuoteResponse
        {
            IsConfigured = true,
            BaseFare = baseFare,
            AirportPickupSupplement = airportPickupSupplement,
            FreeWaitingMinutes = waitingRule.FreeWaitingMinutes,
            ChargeableWaitingMinutes = chargeableWaitingMinutes,
            WaitingChargePerHour = Rate(waitingRule),
            WaitingCharge = waitingCharge,
            ExtrasTotal = extrasTotal,
            DiscountTotal = discounts,
            MinimumFare = minimum,
            Breakdown = breakdown,
            TotalFare = total
        };
    }

    private static decimal BaseFare(PricingRule rule, QuoteRequest request) => rule.RuleType switch
    {
        PricingRuleType.HourlyHire => Math.Round(Rate(rule) * request.HireHours, 2, MidpointRounding.AwayFromZero),
        PricingRuleType.Distance => Math.Round(Rate(rule) * request.DistanceMiles, 2, MidpointRounding.AwayFromZero),
        _ => rule.Amount != 0 ? rule.Amount : rule.BasePrice
    };
    private static decimal Rate(PricingRule rule) => rule.UnitRate != 0 ? rule.UnitRate : rule.WaitingChargePerHour;
    private static decimal Surcharge(PricingRule rule, decimal basis) => rule.Amount != 0 ? rule.Amount : Math.Round(basis * rule.Percentage / 100m, 2, MidpointRounding.AwayFromZero);
    private static bool IsNight(DateTimeOffset? time) => time is { } value && (value.Hour >= 22 || value.Hour < 6);
    private static bool IsWeekend(DateTimeOffset? time) => time is { } value && value.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    private static string Name(PricingRule rule) => string.IsNullOrWhiteSpace(rule.Name) ? rule.RuleType.ToString() : rule.Name;
    private static void Add(List<PricingBreakdownItemDto> breakdown, string name, decimal amount, Guid ruleId) { if (amount != 0) breakdown.Add(new(name, amount, ruleId)); }
}
