using LondonVIP.Infrastructure.Data;
using LondonVIP.Shared.Pricing;
using Microsoft.EntityFrameworkCore;
using LondonVIP.Shared.Tenancy;

namespace LondonVIP.Infrastructure.Pricing;

public class PricingService(LondonVIPDbContext dbContext, ICompanyContext companyContext) : IPricingService
{
    public async Task<QuoteResponse> CalculateQuoteAsync(
        QuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var rule = await dbContext.PricingRules
            .AsNoTracking()
            .FirstOrDefaultAsync(
                pricingRule => pricingRule.IsActive
                    && pricingRule.CompanyId == companyContext.CompanyId
                    && pricingRule.AirportId == request.AirportId
                    && pricingRule.VehicleType == request.VehicleType,
                cancellationToken);

        return PricingCalculator.Calculate(request, rule);
    }
}
