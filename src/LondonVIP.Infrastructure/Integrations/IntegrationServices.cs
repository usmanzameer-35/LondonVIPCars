using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using LondonVIP.Shared.Integrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LondonVIP.Infrastructure.Integrations;

public sealed class ProviderRegistry(IEnumerable<IIntegrationProvider> providers, IConfiguration configuration) : IProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IIntegrationProvider> providers = providers.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<ProviderDescriptor> Describe() => providers.Values.OrderBy(x => x.Category).ThenBy(x => x.Key).Select(x =>
        new ProviderDescriptor(x.Key, x.Key, x.Category, configuration.GetValue<bool>($"Integrations:{x.Key}:Configured"), configuration.GetValue($"Integrations:{x.Key}:Enabled", true))).ToList();
    public IIntegrationProvider? Find(string key) => providers.GetValueOrDefault(key);
}

public sealed class IntegrationExecutionPolicy(ILogger<IntegrationExecutionPolicy> logger) : IIntegrationExecutionPolicy
{
    public async Task<T> ExecuteAsync<T>(string providerKey, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        for (var attempt = 1; ; attempt++)
        {
            try { return await action(cancellationToken); }
            catch (Exception exception) when (attempt < 3 && exception is not OperationCanceledException)
            { logger.LogWarning(exception, "Integration {ProviderKey} failed on attempt {Attempt}; retrying.", providerKey, attempt); }
        }
    }
}

public abstract class UnconfiguredProvider(string key, IntegrationCategory category) : IIntegrationProvider
{
    public string Key { get; } = key;
    public IntegrationCategory Category { get; } = category;
    public virtual Task<ProviderHealthDto> CheckHealthAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ProviderHealthDto(Key, Category, IntegrationHealthState.NotConfigured, 0, 0, 0, "Provider is not configured.", DateTimeOffset.UtcNow));
    protected static InvalidOperationException NotConfigured(string key) => new($"Integration provider '{key}' is not configured.");
}

public sealed class UnconfiguredPaymentProvider() : UnconfiguredProvider("payments", IntegrationCategory.Payments), IIntegrationPaymentProvider
{ public Task<PaymentProviderResult> AuthorizeAsync(PaymentProviderRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new PaymentProviderResult(false, null, "Payment provider is not configured.")); }
public sealed class UnconfiguredMapsProvider() : UnconfiguredProvider("maps", IntegrationCategory.Maps), IGeocodingProvider, IRoutingProvider, IDistanceMatrixProvider, IPlacesProvider
{
    public Task<IntegrationCoordinate?> GeocodeAsync(string address, CancellationToken cancellationToken = default) => Task.FromResult<IntegrationCoordinate?>(null);
    public Task<string?> ReverseGeocodeAsync(IntegrationCoordinate coordinate, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    public Task<IntegrationRouteResult> DirectionsAsync(IntegrationRouteRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new IntegrationRouteResult(false, 0, 0, "Maps provider is not configured."));
    public Task<int?> EtaAsync(IntegrationRouteRequest request, CancellationToken cancellationToken = default) => Task.FromResult<int?>(null);
    public Task<IReadOnlyList<IReadOnlyList<double>>> CalculateAsync(IReadOnlyList<IntegrationCoordinate> origins, IReadOnlyList<IntegrationCoordinate> destinations, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<IReadOnlyList<double>>>([]);
    public Task<IReadOnlyList<string>> SearchAsync(string query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);
    public Task<byte[]?> StaticMapAsync(IntegrationCoordinate centre, CancellationToken cancellationToken = default) => Task.FromResult<byte[]?>(null);
}
public abstract class DevelopmentCommunicationProvider(string key) : UnconfiguredProvider(key, IntegrationCategory.Communications), ICommunicationProvider
{
    public IReadOnlyCollection<string> Templates => [];
    public Task<CommunicationResult> SendAsync(CommunicationRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new CommunicationResult(false, null, $"{Key} provider is not configured."));
    public Task<CommunicationResult> RetryAsync(string providerReference, CancellationToken cancellationToken = default) => Task.FromResult(new CommunicationResult(false, providerReference, $"{Key} provider is not configured."));
    public Task<DeliveryReport?> GetStatusAsync(string providerReference, CancellationToken cancellationToken = default) => Task.FromResult<DeliveryReport?>(null);
}
public sealed class UnconfiguredWhatsAppProvider() : DevelopmentCommunicationProvider("whatsapp"), IWhatsAppProvider;
public sealed class UnconfiguredPushProvider() : DevelopmentCommunicationProvider("push"), IPushNotificationProvider;
public sealed class UnconfiguredVoiceProvider() : DevelopmentCommunicationProvider("voice"), IVoiceCallingProvider;
public sealed class UnconfiguredFlightProvider() : UnconfiguredProvider("flights", IntegrationCategory.Flights), IFlightDataProvider
{ public Task<FlightDataResult?> LookupAsync(string flightNumber, DateOnly date, CancellationToken cancellationToken = default) => Task.FromResult<FlightDataResult?>(null); public Task<IReadOnlyDictionary<string, string>> GetAirportMetadataAsync(string airportCode, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>()); }
public sealed class UnconfiguredStorageProvider() : UnconfiguredProvider("storage", IntegrationCategory.Storage), IFileStorageProvider
{
    public Task<string> SaveAsync(string path, Stream content, string contentType, CancellationToken cancellationToken = default) => Task.FromException<string>(NotConfigured(Key));
    public Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult<Stream?>(null);
    public Task DeleteAsync(string path, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
public sealed class UnconfiguredPdfProvider() : UnconfiguredProvider("pdf", IntegrationCategory.Pdf), IPdfGenerationProvider
{ public Task<byte[]> GenerateAsync(PdfDocumentType documentType, object model, CancellationToken cancellationToken = default) => Task.FromException<byte[]>(NotConfigured(Key)); }

public sealed class ConfigurationSecretProvider(IConfiguration configuration) : ISecretProvider
{
    public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(configuration[$"Integrations:Secrets:{name}"] ?? configuration[name]);
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
}

public sealed class HmacWebhookSignatureValidator : IWebhookSignatureValidator
{
    public bool Validate(string payload, string signature, string secret)
    {
        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(secret)) return false;
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
        try { return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(signature)); }
        catch (FormatException) { return false; }
    }
}

public sealed class InMemoryWebhookEngine(IWebhookSignatureValidator signatures, ISecretProvider secrets, ILogger<InMemoryWebhookEngine> logger) : IWebhookEngine
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> received = new(StringComparer.Ordinal);
    private int pending;
    private int failed;
    public int PendingCount => pending;
    public int FailedCount => failed;
    public async Task<WebhookResult> ReceiveAsync(string providerKey, string eventType, string payload, string? signature, string deliveryId, CancellationToken cancellationToken = default)
    {
        if (!received.TryAdd($"{providerKey}:{deliveryId}", DateTimeOffset.UtcNow)) return new(false, true, "Webhook delivery was already processed.");
        var secret = await secrets.GetSecretAsync($"{providerKey}:WebhookSecret", cancellationToken);
        if (secret is not null && (signature is null || !signatures.Validate(payload, signature, secret))) { Interlocked.Increment(ref failed); return new(false, false, "Webhook signature is invalid."); }
        logger.LogInformation("Accepted {ProviderKey} webhook {EventType} with delivery {DeliveryId}.", providerKey, eventType, deliveryId);
        return new(true, false, "Webhook accepted.");
    }
    public Task<WebhookResult> SendAsync(string providerKey, string eventType, string payload, CancellationToken cancellationToken = default)
    { Interlocked.Increment(ref pending); logger.LogInformation("Queued outgoing {ProviderKey} webhook {EventType}.", providerKey, eventType); return Task.FromResult(new WebhookResult(true, false, "Webhook queued for a future provider.")); }
}

public sealed class IntegrationDiagnosticsService(IProviderRegistry registry, ISecretProvider secrets, IWebhookEngine webhooks)
{
    public async Task<IntegrationDashboardDto> GetDashboardAsync(CancellationToken token = default)
    {
        var descriptors = registry.Describe(); var health = new List<ProviderHealthDto>();
        foreach (var item in descriptors) { var provider = registry.Find(item.Key)!; health.Add(await provider.CheckHealthAsync(token)); }
        return new(descriptors, health, webhooks.PendingCount, webhooks.FailedCount, await secrets.IsAvailableAsync(token));
    }
    public async Task<ConnectionTestResult> TestAsync(string key, CancellationToken token = default)
    {
        var provider = registry.Find(key); if (provider is null) return new(key, false, "Provider is unknown.", 0);
        var watch = Stopwatch.StartNew(); var health = await provider.CheckHealthAsync(token); watch.Stop();
        return new(key, health.State is IntegrationHealthState.Healthy or IntegrationHealthState.Degraded, health.Message, watch.ElapsedMilliseconds);
    }
}
