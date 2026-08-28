using System.Net;
using System.Net.Http.Json;
using LondonVIP.Infrastructure.Pricing;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Pricing;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace LondonVIP.Tests;

public class QuotesEndpointTests
{
    [Fact]
    public async Task PostQuote_ReturnsPricingNotConfigured_WhenNoActiveRuleExists()
    {
        await using var app = LondonVIP.Api.Program.CreateApp(["--environment", "Development"]);
        app.Urls.Add("http://127.0.0.1:0");

        await app.StartAsync();

        try
        {
            var server = app.Services.GetRequiredService<IServer>();
            var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();

            using var handler = new HttpClientHandler { UseProxy = false };
            using var client = new HttpClient(handler) { BaseAddress = new Uri(address) };
            using var response = await client.PostAsJsonAsync("/api/quotes", new QuoteRequest
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
        finally
        {
            await app.StopAsync();
        }
    }
}
