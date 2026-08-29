using System.Net;
using System.Net.Http.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Pricing;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Pricing;
using LondonVIP.Shared.Security;
using LondonVIP.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LondonVIP.Tests;

public class PricingAdministrationEndpointTests
{
    [Fact]
    public async Task AdminCanListOnlyTenantPricingRules()
    {
        await using var host = await TestApiHost.StartAsync();
        var current = await AddRuleAsync(host, LondonVipCompany.Id, true, VehicleType.Saloon);
        var other = await AddOtherTenantRuleAsync(host);
        var rules = await host.Client.GetFromJsonAsync<List<PricingRuleListItemDto>>("/api/pricing");
        Assert.Contains(rules!, item => item.Id == current.Id);
        Assert.DoesNotContain(rules!, item => item.Id == other.Id);
    }

    [Fact]
    public async Task AdminCanCreateAndReadValidRule_AndCreationIsAudited()
    {
        await using var host = await TestApiHost.StartAsync();
        var airport = await FirstAirportAsync(host);
        using var response = await host.Client.PostAsJsonAsync("/api/pricing", Valid(airport, VehicleType.Estate));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var rule = await response.Content.ReadFromJsonAsync<PricingRuleDetailDto>();
        Assert.NotNull(rule); Assert.Equal(airport, rule.AirportId); Assert.Equal(50m, rule.BasePrice); Assert.NotEqual(default, rule.CreatedAt);
        var detail = await host.Client.GetFromJsonAsync<PricingRuleDetailDto>($"/api/pricing/{rule.Id}");
        Assert.Equal(rule.Id, detail?.Id);
        await using var scope = host.App.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        Assert.Contains(db.SecurityAuditEvents, item => item.EventType == "PricingRuleCreated" && item.ResourceIdentifier == rule.Id.ToString());
    }

    [Fact]
    public async Task AdminCanUpdateTenantRule_AndUpdateIsAudited()
    {
        await using var host = await TestApiHost.StartAsync();
        var rule = await AddRuleAsync(host, LondonVipCompany.Id, false, VehicleType.MPV);
        var update = Valid(null, VehicleType.MPV); update.BasePrice = 82.50m; update.IsActive = false;
        using var response = await host.Client.PutAsJsonAsync($"/api/pricing/{rule.Id}", update);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var changed = await response.Content.ReadFromJsonAsync<PricingRuleDetailDto>(); Assert.Equal(82.50m, changed?.BasePrice);
        await using var scope = host.App.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        Assert.Contains(db.SecurityAuditEvents, item => item.EventType == "PricingRuleUpdated" && item.ResourceIdentifier == rule.Id.ToString());
    }

    [Fact]
    public async Task InvalidRuleAndUnknownAirportReturnValidationProblems()
    {
        await using var host = await TestApiHost.StartAsync();
        var invalid = Valid(Guid.NewGuid(), (VehicleType)999); invalid.BasePrice = -1; invalid.AirportPickupSupplement = 10001; invalid.FreeWaitingMinutes = 1441; invalid.WaitingChargePerHour = -1;
        using var response = await host.Client.PostAsJsonAsync("/api/pricing", invalid);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationResponse>();
        Assert.Contains("airportId", problem!.Errors.Keys, StringComparer.OrdinalIgnoreCase); Assert.Contains("vehicleType", problem.Errors.Keys, StringComparer.OrdinalIgnoreCase); Assert.Contains("basePrice", problem.Errors.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DuplicateActiveRuleIsRejected_ButInactiveRuleIsAllowed()
    {
        await using var host = await TestApiHost.StartAsync();
        await AddRuleAsync(host, LondonVipCompany.Id, true, VehicleType.EightSeater);
        using var duplicate = await host.Client.PostAsJsonAsync("/api/pricing", Valid(null, VehicleType.EightSeater));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        var inactive = Valid(null, VehicleType.EightSeater); inactive.IsActive = false;
        using var inactiveResponse = await host.Client.PostAsJsonAsync("/api/pricing", inactive);
        Assert.Equal(HttpStatusCode.Created, inactiveResponse.StatusCode);
    }

    [Fact]
    public async Task ActivateAndDeactivateAffectQuoteEngineAndCreateAuditEvents()
    {
        await using var host = await TestApiHost.StartAsync();
        var rule = await AddRuleAsync(host, LondonVipCompany.Id, true, VehicleType.Saloon, 64m);
        var quote = await QuoteAsync(host, VehicleType.Saloon);
        Assert.True(quote.IsConfigured); Assert.Equal(64m, quote.BaseFare);
        using (var deactivated = await PatchStatusAsync(host, rule.Id, false)) Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        Assert.False((await QuoteAsync(host, VehicleType.Saloon)).IsConfigured);
        using (var activated = await PatchStatusAsync(host, rule.Id, true)) Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
        Assert.True((await QuoteAsync(host, VehicleType.Saloon)).IsConfigured);
        await using var scope = host.App.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        Assert.Contains(db.SecurityAuditEvents, item => item.EventType == "PricingRuleDeactivated" && item.ResourceIdentifier == rule.Id.ToString());
        Assert.Contains(db.SecurityAuditEvents, item => item.EventType == "PricingRuleActivated" && item.ResourceIdentifier == rule.Id.ToString());
    }

    [Fact]
    public async Task CrossTenantRuleReturnsNotFoundAndAttemptIsAudited()
    {
        await using var host = await TestApiHost.StartAsync();
        var other = await AddOtherTenantRuleAsync(host);
        using var get = await host.Client.GetAsync($"/api/pricing/{other.Id}");
        using var put = await host.Client.PutAsJsonAsync($"/api/pricing/{other.Id}", Valid(null, VehicleType.Saloon));
        using var status = await PatchStatusAsync(host, other.Id, false);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode); Assert.Equal(HttpStatusCode.NotFound, put.StatusCode); Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);
        await using var scope = host.App.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        Assert.Contains(db.SecurityAuditEvents, item => item.EventType == "CrossTenantAccessAttempt" && item.ResourceIdentifier == other.Id.ToString());
    }

    [Theory]
    [InlineData(SecurityRoles.Dispatcher, HttpStatusCode.OK, HttpStatusCode.Forbidden)]
    [InlineData(SecurityRoles.Finance, HttpStatusCode.OK, HttpStatusCode.Forbidden)]
    [InlineData(SecurityRoles.Driver, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden)]
    public async Task PricingAuthorizationUsesLeastPrivilege(string role, HttpStatusCode readStatus, HttpStatusCode writeStatus)
    {
        await using var host = await TestApiHost.StartAsync();
        using var read = Request<object>(HttpMethod.Get, "/api/pricing", role);
        using var readResponse = await host.Client.SendAsync(read); Assert.Equal(readStatus, readResponse.StatusCode);
        using var write = Request(HttpMethod.Post, "/api/pricing", role, Valid(null, VehicleType.Estate));
        using var writeResponse = await host.Client.SendAsync(write); Assert.Equal(writeStatus, writeResponse.StatusCode);
    }

    [Fact]
    public async Task SummaryAndAirportsUseRealDatabaseConfiguration()
    {
        await using var host = await TestApiHost.StartAsync();
        var airport = await FirstAirportAsync(host);
        await AddRuleAsync(host, LondonVipCompany.Id, true, VehicleType.Saloon, airportId: airport);
        await AddRuleAsync(host, LondonVipCompany.Id, false, VehicleType.MPV);
        var summary = await host.Client.GetFromJsonAsync<PricingSummaryDto>("/api/pricing/summary");
        var airports = await host.Client.GetFromJsonAsync<List<PricingAirportLookupDto>>("/api/pricing/airports");
        Assert.Equal(1, summary?.ActiveRules); Assert.Equal(1, summary?.InactiveRules); Assert.Equal(1, summary?.AirportsConfigured); Assert.Equal(4, airports?.Count);
    }

    private static PricingRuleUpdateDto Valid(Guid? airport, VehicleType vehicle) => new() { AirportId = airport, VehicleType = vehicle, BasePrice = 50m, AirportPickupSupplement = 10m, FreeWaitingMinutes = 30, WaitingChargePerHour = 40m, IsActive = true };
    private static async Task<Guid> FirstAirportAsync(TestApiHost host) { await using var scope = host.App.Services.CreateAsyncScope(); return scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>().Airports.Select(item => item.Id).First(); }
    private static async Task<PricingRule> AddRuleAsync(TestApiHost host, Guid companyId, bool active, VehicleType vehicle, decimal basePrice = 50m, Guid? airportId = null) { var now = DateTimeOffset.UtcNow; var rule = new PricingRule { Id = Guid.NewGuid(), CompanyId = companyId, AirportId = airportId, VehicleType = vehicle, BasePrice = basePrice, AirportPickupSupplement = 10m, FreeWaitingMinutes = 30, WaitingChargePerHour = 40m, IsActive = active, CreatedAt = now, UpdatedAt = now }; await using var scope = host.App.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>(); db.PricingRules.Add(rule); await db.SaveChangesAsync(); return rule; }
    private static async Task<PricingRule> AddOtherTenantRuleAsync(TestApiHost host) { var id = Guid.NewGuid(); var company = new Company { Id = id, TradingName = "Other Cars", LegalName = "Other Cars", Slug = $"other-{id:N}", Email = "", Phone = "", WebsiteUrl = "", AddressLine1 = "", AddressLine2 = "", City = "London", Postcode = "", Country = "UK", TimeZone = "Europe/London", CurrencyCode = "GBP", IsActive = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }; await using (var scope = host.App.Services.CreateAsyncScope()) { var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>(); db.Companies.Add(company); await db.SaveChangesAsync(); } return await AddRuleAsync(host, id, true, VehicleType.Saloon); }
    private static async Task<QuoteResponse> QuoteAsync(TestApiHost host, VehicleType vehicle) { using var response = await host.Client.PostAsJsonAsync("/api/quotes", new QuoteRequest { PickupAddress = "Pickup", Destination = "Destination", VehicleType = vehicle, PassengerCount = 1, LuggageCount = 0 }); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<QuoteResponse>())!; }
    private static Task<HttpResponseMessage> PatchStatusAsync(TestApiHost host, Guid id, bool active) => host.Client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"/api/pricing/{id}/status") { Content = JsonContent.Create(new PricingRuleStatusDto { IsActive = active }) });
    private static HttpRequestMessage Request<T>(HttpMethod method, string uri, string role, T? body = default) { var request = new HttpRequestMessage(method, uri); request.Headers.Add("X-Test-Role", role); if (body is not null) request.Content = JsonContent.Create(body); return request; }
    private sealed class ValidationResponse { public Dictionary<string, string[]> Errors { get; set; } = []; }
}
