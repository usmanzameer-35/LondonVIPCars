using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using LondonVIP.Infrastructure.Integrations;
using LondonVIP.Shared.Integrations;
using LondonVIP.Shared.Security;
using LondonVIP.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LondonVIP.Tests;

public sealed class IntegrationFrameworkTests
{
    [Fact]
    public async Task ProviderContractsAreRegisteredAndDashboardIsSecured()
    {
        await using var host = await TestApiHost.StartAsync();
        await using var scope = host.App.Services.CreateAsyncScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IProviderRegistry>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IIntegrationPaymentProvider>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IGeocodingProvider>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IFlightDataProvider>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IFileStorageProvider>());

        using var forbidden = new HttpRequestMessage(HttpMethod.Get, "/api/integrations");
        forbidden.Headers.Add("X-Test-Role", SecurityRoles.Admin);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.SendAsync(forbidden)).StatusCode);
        using var anonymous = new HttpRequestMessage(HttpMethod.Get, "/api/integrations");
        anonymous.Headers.Add("X-Test-Anonymous", "true");
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.SendAsync(anonymous)).StatusCode);

        using var authorized = new HttpRequestMessage(HttpMethod.Get, "/api/integrations");
        authorized.Headers.Add("X-Test-Role", SecurityRoles.SuperAdmin);
        using var response = await host.Client.SendAsync(authorized);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dashboard = await response.Content.ReadFromJsonAsync<IntegrationDashboardDto>();
        Assert.NotNull(dashboard);
        Assert.NotEmpty(dashboard.Providers);
        Assert.All(dashboard.Health, x => Assert.Equal(IntegrationHealthState.NotConfigured, x.State));
    }

    [Fact]
    public async Task RetryPolicyRetriesTransientFailures()
    {
        var policy = new IntegrationExecutionPolicy(NullLogger<IntegrationExecutionPolicy>.Instance);
        var attempts = 0;
        var result = await policy.ExecuteAsync("test", _ => ++attempts < 3 ? Task.FromException<int>(new InvalidOperationException("transient")) : Task.FromResult(42));
        Assert.Equal(42, result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task WebhookValidationRejectsInvalidSignaturesAndPreventsReplay()
    {
        const string secret = "development-test-secret";
        const string payload = "{\"event\":\"booking.updated\"}";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Integrations:Secrets:test:WebhookSecret"] = secret }).Build();
        var engine = new InMemoryWebhookEngine(new HmacWebhookSignatureValidator(), new ConfigurationSecretProvider(configuration), NullLogger<InMemoryWebhookEngine>.Instance);
        var invalid = await engine.ReceiveAsync("test", "booking.updated", payload, "invalid", "tenant-a:delivery-1");
        Assert.False(invalid.Accepted);
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload)));
        var accepted = await engine.ReceiveAsync("test", "booking.updated", payload, signature, "tenant-a:delivery-2");
        Assert.True(accepted.Accepted);
        var replay = await engine.ReceiveAsync("test", "booking.updated", payload, signature, "tenant-a:delivery-2");
        Assert.True(replay.Duplicate);
        var otherTenant = await engine.ReceiveAsync("test", "booking.updated", payload, signature, "tenant-b:delivery-2");
        Assert.True(otherTenant.Accepted);
    }

    [Fact]
    public async Task SecretProviderLoadsConfigurationWithoutExposingItInDashboardContracts()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Integrations:Secrets:ApiKey"] = "not-returned" }).Build();
        var provider = new ConfigurationSecretProvider(configuration);
        Assert.Equal("not-returned", await provider.GetSecretAsync("ApiKey"));
        Assert.DoesNotContain(typeof(IntegrationDashboardDto).GetProperties(), x => x.Name.Contains("Secret", StringComparison.Ordinal) && x.PropertyType == typeof(string));
    }
}
