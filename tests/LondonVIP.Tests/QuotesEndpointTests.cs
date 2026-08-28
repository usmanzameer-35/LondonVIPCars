using System.Net;
using System.Net.Http.Json;
using LondonVIP.Infrastructure.Pricing;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Pricing;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using LondonVIP.Tests.Infrastructure;

namespace LondonVIP.Tests;

public class QuotesEndpointTests
{
    [Fact]
    public async Task PostQuote_ReturnsPricingNotConfigured_WhenNoActiveRuleExists()
    {
        await using var host = await TestApiHost.StartAsync();
        using var response = await host.Client.PostAsJsonAsync("/api/quotes", new QuoteRequest
            {
                PickupAddress = "Test pickup",
                Destination = "Test destination",
                VehicleType = VehicleType.Saloon,
                PassengerCount = 2,
                LuggageCount = 1
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var quote = await response.Content.ReadFromJsonAsync<QuoteResponse>();
        Assert.NotNull(quote);
        Assert.False(quote.IsConfigured);
        Assert.Equal(PricingCalculator.PricingNotConfiguredMessage, quote.Message);
        Assert.Equal(0m, quote.TotalFare);
    }
}
