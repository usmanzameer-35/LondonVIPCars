using System.Net;
using System.Net.Http.Json;
using LondonVIP.Infrastructure.Pricing;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Pricing;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using LondonVIP.Tests.Infrastructure;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Tenancy;

namespace LondonVIP.Tests;

public class QuotesEndpointTests
{
    [Fact]
    public async Task PostQuote_UsesCurrentEffectiveTenantRuleAndIgnoresExpiredDisabledAndOtherTenantRules()
    {
        await using var host = await TestApiHost.StartAsync();
        var now = DateTimeOffset.UtcNow;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
            var otherId = Guid.NewGuid();
            db.Companies.Add(new Company { Id=otherId,TradingName="Other",LegalName="Other",Slug=$"other-{otherId:N}",City="London",Country="UK",TimeZone="Europe/London",CurrencyCode="GBP",IsActive=true,CreatedAt=now,UpdatedAt=now });
            db.PricingRules.AddRange(
                V2Rule(LondonVipCompany.Id, 70, 10, true, now.AddDays(-1), now.AddDays(1)),
                V2Rule(LondonVipCompany.Id, 900, 100, true, now.AddDays(-3), now.AddDays(-2)),
                V2Rule(LondonVipCompany.Id, 800, 100, false, null, null),
                V2Rule(otherId, 999, 1000, true, null, null));
            await db.SaveChangesAsync();
        }
        using var response = await host.Client.PostAsJsonAsync("/api/quotes", new QuoteRequest { PickupAddress="A",Destination="B",PickupZone="West",DestinationZone="Central",VehicleType=VehicleType.Estate,PassengerCount=1,JourneyDateTime=now });
        var quote = await response.Content.ReadFromJsonAsync<QuoteResponse>();
        Assert.True(quote?.IsConfigured); Assert.Equal(70m, quote?.TotalFare);
    }

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

    private static PricingRule V2Rule(Guid companyId, decimal amount, int priority, bool active, DateTimeOffset? from, DateTimeOffset? to) => new()
    {
        Id=Guid.NewGuid(),CompanyId=companyId,RuleType=PricingRuleType.ZoneFixedFare,Name="Scheduled zone fare",VehicleType=VehicleType.Estate,PickupZone="West",DestinationZone="Central",
        Amount=amount,Priority=priority,IsActive=active,EffectiveFrom=from,EffectiveTo=to,CreatedAt=DateTimeOffset.UtcNow,UpdatedAt=DateTimeOffset.UtcNow
    };
}
