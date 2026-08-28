using LondonVIP.Infrastructure.Data;
using LondonVIP.Shared.Pricing;
using Microsoft.EntityFrameworkCore;

namespace LondonVIP.Infrastructure.Pricing;

public class PricingService(LondonVIPDbContext dbContext) : IPricingService
{
    public async Task<QuoteResponse> CalculateQuoteAsync(
        QuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var rule = await dbContext.PricingRules
            .AsNoTracking()
            .FirstOrDefaultAsync(
                pricingRule => pricingRule.IsActive
                    && pricingRule.AirportId == request.AirportId
                    && pricingRule.VehicleType == request.VehicleType,
                cancellationToken);

        return PricingCalculator.Calculate(request, rule);
    }
}
