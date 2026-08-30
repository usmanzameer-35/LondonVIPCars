using LondonVIP.Infrastructure.Data;
using LondonVIP.Shared.Pricing;
using Microsoft.EntityFrameworkCore;
using LondonVIP.Shared.Tenancy;
using LondonVIP.Shared.Models;

namespace LondonVIP.Infrastructure.Pricing;

public class PricingService(LondonVIPDbContext dbContext, ICompanyContext companyContext) : IPricingService
{
    public async Task<QuoteResponse> CalculateQuoteAsync(
        QuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var evaluationTime = request.JourneyDateTime ?? DateTimeOffset.UtcNow;
        var candidates = await dbContext.PricingRules
            .AsNoTracking()
            .Where(
                pricingRule => pricingRule.IsActive
                    && pricingRule.CompanyId == companyContext.CompanyId
                    && pricingRule.VehicleType == request.VehicleType)
            .ToListAsync(cancellationToken);

        var rules = candidates.Where(rule => (!rule.EffectiveFrom.HasValue || rule.EffectiveFrom <= evaluationTime)
                && (!rule.EffectiveTo.HasValue || rule.EffectiveTo >= evaluationTime)
                && Matches(rule, request)).ToList();
        return PricingCalculator.CalculateRules(request, rules);
    }

    private static bool Matches(PricingRule rule, QuoteRequest request) => rule.RuleType switch
    {
        PricingRuleType.LegacyFare or PricingRuleType.AirportFixedFare => rule.AirportId == request.AirportId,
        PricingRuleType.PostcodeFixedFare => Same(rule.PickupPostcode, request.PickupPostcode) && Same(rule.DestinationPostcode, request.DestinationPostcode),
        PricingRuleType.ZoneFixedFare => Same(rule.PickupZone, request.PickupZone) && Same(rule.DestinationZone, request.DestinationZone),
        PricingRuleType.MeetAndGreet => request.IsMeetAndGreet,
        PricingRuleType.CorporateDiscount => request.IsCorporateCustomer,
        PricingRuleType.PromotionalDiscount => Same(rule.PromotionCode, request.PromotionCode),
        PricingRuleType.HolidaySurcharge => request.IsHoliday,
        PricingRuleType.CancellationFee => request.IsCancellation,
        _ => true
    };

    private static bool Same(string? configured, string? requested) =>
        !string.IsNullOrWhiteSpace(configured) && string.Equals(configured.Trim(), requested?.Trim(), StringComparison.OrdinalIgnoreCase);
}
