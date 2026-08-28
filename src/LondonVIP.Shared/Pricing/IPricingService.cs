namespace LondonVIP.Shared.Pricing;

public interface IPricingService
{
    Task<QuoteResponse> CalculateQuoteAsync(
        QuoteRequest request,
        CancellationToken cancellationToken = default);
}
