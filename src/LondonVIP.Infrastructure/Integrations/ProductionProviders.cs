using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Shared.Integrations;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using LondonVIP.Shared.CustomerPortal;

namespace LondonVIP.Infrastructure.Integrations;

internal static class IntegrationHttp
{
    public static async Task<JsonDocument> JsonAsync(HttpResponseMessage response, CancellationToken token)
    {
        var content = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Provider returned {(int)response.StatusCode}: {SafeError(content)}", null, response.StatusCode);
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(content) ? "{}" : content);
    }
    public static string SafeError(string value) => value.Length > 500 ? value[..500] : value;
    public static string? String(JsonElement element, params string[] path) { foreach (var name in path) { if (!element.TryGetProperty(name, out element)) return null; } return element.ValueKind == JsonValueKind.Null ? null : element.ToString(); }
    public static FormUrlEncodedContent Form(params (string Key, string? Value)[] values) => new(values.Where(x => x.Value is not null).Select(x => new KeyValuePair<string, string>(x.Key, x.Value!)));
}

public abstract class ProductionProviderBase(
    string key, IntegrationCategory category, IConfiguration configuration, IHttpClientFactory clients,
    IIntegrationExecutionPolicy execution, LondonVIPDbContext db, ICompanyContext company, ILogger logger) : IIntegrationProvider
{
    protected IConfiguration Configuration { get; } = configuration;
    protected HttpClient Client => clients.CreateClient("LondonVIP.Integrations");
    protected IIntegrationExecutionPolicy Execution { get; } = execution;
    protected LondonVIPDbContext Db { get; } = db;
    protected Guid CompanyId => company.CompanyId;
    protected ILogger Logger { get; } = logger;
    public string Key { get; } = key;
    public IntegrationCategory Category { get; } = category;
    protected string? Secret(string name) => Configuration[$"Integrations:{Key}:{name}"];
    protected bool Configured(params string[] required) => required.All(x => !string.IsNullOrWhiteSpace(Secret(x)));

    protected async Task<T> ObserveAsync<T>(string operation, Func<CancellationToken, Task<T>> action, CancellationToken token)
    {
        var watch = Stopwatch.StartNew(); var success = false; string? error = null;
        try { var result = await Execution.ExecuteAsync(Key, action, token); success = true; return result; }
        catch (Exception exception) { error = exception.GetType().Name; Logger.LogError(exception, "{Provider} {Operation} failed.", Key, operation); throw; }
        finally
        {
            watch.Stop();
            Db.IntegrationProviderMetrics.Add(new IntegrationProviderMetric { Id = Guid.NewGuid(), CompanyId = CompanyId, ProviderKey = Key, Operation = operation, Success = success, LatencyMilliseconds = watch.ElapsedMilliseconds, ErrorCode = error, CorrelationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"), OccurredAt = DateTimeOffset.UtcNow });
            await Db.SaveChangesAsync(CancellationToken.None);
        }
    }

    public virtual async Task<ProviderHealthDto> CheckHealthAsync(CancellationToken token = default)
    {
        if (!IsConfigured) return new(Key, Category, IntegrationHealthState.NotConfigured, 0, 0, 0, "Provider is not configured.", DateTimeOffset.UtcNow);
        var since = DateTimeOffset.UtcNow.AddHours(-24);
        var metrics = await Db.IntegrationProviderMetrics.AsNoTracking().Where(x => x.CompanyId == CompanyId && x.ProviderKey == Key && x.OccurredAt >= since).GroupBy(_ => 1).Select(x => new { Failures = x.Count(y => !y.Success), Retries = x.Sum(y => y.RetryCount), Average = x.Average(y => y.LatencyMilliseconds) }).SingleOrDefaultAsync(token);
        return new(Key, Category, metrics?.Failures > 0 ? IntegrationHealthState.Degraded : IntegrationHealthState.Healthy, (long)(metrics?.Average ?? 0), metrics?.Failures ?? 0, metrics?.Retries ?? 0, metrics?.Failures > 0 ? "Provider has recent failures." : "Provider is configured.", DateTimeOffset.UtcNow);
    }
    protected abstract bool IsConfigured { get; }
}

public sealed class StripePaymentProvider(IConfiguration configuration, IHttpClientFactory clients, IIntegrationExecutionPolicy execution, LondonVIPDbContext db, ICompanyContext company, ILogger<StripePaymentProvider> logger)
    : ProductionProviderBase("stripe", IntegrationCategory.Payments, configuration, clients, execution, db, company, logger), IPaymentLifecycleProvider
{
    protected override bool IsConfigured => Configured("SecretKey", "WebhookSecret");
    private HttpRequestMessage Request(HttpMethod method, string path, HttpContent? content = null, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(method, $"https://api.stripe.com/v1/{path}") { Content = content };
        request.Headers.Authorization = new("Bearer", Secret("SecretKey"));
        if (!string.IsNullOrWhiteSpace(idempotencyKey)) request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        return request;
    }
    public async Task<CustomerPaymentIntentResult> CreateCustomerIntentAsync(CustomerPaymentIntentRequest request, CancellationToken token = default)
    {
        if (!IsConfigured) return new(false, null, "Unavailable", null, "Stripe is not configured.");
        if (request.Amount <= 0 || string.IsNullOrWhiteSpace(request.IdempotencyKey)) return new(false, null, "Invalid", null, "Payment request is invalid.");
        return await ObserveAsync("PaymentIntent.CustomerCreate", async ct => { using var message = Request(HttpMethod.Post, "payment_intents", IntegrationHttp.Form(("amount", decimal.Round(request.Amount * 100m).ToString("0", CultureInfo.InvariantCulture)), ("currency", "gbp"), ("automatic_payment_methods[enabled]", "true"), ("metadata[invoice_id]", request.InvoiceId.ToString()), ("metadata[method]", request.Method)), request.IdempotencyKey); using var response = await Client.SendAsync(message, ct); using var json = await IntegrationHttp.JsonAsync(response, ct); return new CustomerPaymentIntentResult(true, IntegrationHttp.String(json.RootElement, "id"), IntegrationHttp.String(json.RootElement, "status") ?? "requires_payment_method", IntegrationHttp.String(json.RootElement, "client_secret"), null); }, token);
    }
    public async Task<PaymentProviderResult> AuthorizeAsync(PaymentProviderRequest request, CancellationToken token = default)
    {
        if (!IsConfigured) return new(false, null, "Stripe is not configured.");
        if (request.Amount <= 0 || string.IsNullOrWhiteSpace(request.Reference)) return new(false, null, "Payment request is invalid.");
        return await ObserveAsync("PaymentIntent.Create", async ct =>
        {
            using var message = Request(HttpMethod.Post, "payment_intents", IntegrationHttp.Form(("amount", decimal.Round(request.Amount * 100m).ToString("0", CultureInfo.InvariantCulture)), ("currency", request.CurrencyCode.ToLowerInvariant()), ("payment_method", request.PaymentMethodToken), ("confirm", request.PaymentMethodToken is null ? "false" : "true"), ("automatic_payment_methods[enabled]", "true"), ("metadata[reference]", request.Reference)), request.Reference);
            using var response = await Client.SendAsync(message, ct); using var json = await IntegrationHttp.JsonAsync(response, ct);
            return new PaymentProviderResult(true, IntegrationHttp.String(json.RootElement, "id"), null);
        }, token);
    }
    public async Task<ProviderCustomerResult> CreateCustomerAsync(ProviderCustomerRequest request, string idempotencyKey, CancellationToken token = default)
    {
        if (!IsConfigured) return new(false, null, "Stripe is not configured.");
        return await ObserveAsync("Customer.Create", async ct => { using var message = Request(HttpMethod.Post, "customers", IntegrationHttp.Form(("email", request.Email), ("name", request.Name), ("phone", request.Phone), ("metadata[customer_id]", request.CustomerId.ToString())), idempotencyKey); using var response = await Client.SendAsync(message, ct); using var json = await IntegrationHttp.JsonAsync(response, ct); return new ProviderCustomerResult(true, IntegrationHttp.String(json.RootElement, "id"), null); }, token);
    }
    public async Task<IReadOnlyList<SavedPaymentMethodDto>> GetSavedPaymentMethodsAsync(string providerCustomerId, CancellationToken token = default)
    {
        if (!IsConfigured) return [];
        return await ObserveAsync("PaymentMethods.List", async ct => { using var message = Request(HttpMethod.Get, $"payment_methods?customer={Uri.EscapeDataString(providerCustomerId)}&type=card"); using var response = await Client.SendAsync(message, ct); using var json = await IntegrationHttp.JsonAsync(response, ct); if (!json.RootElement.TryGetProperty("data", out var data)) return []; return data.EnumerateArray().Select(x => new SavedPaymentMethodDto(IntegrationHttp.String(x, "id") ?? "", IntegrationHttp.String(x, "type") ?? "card", $"•••• {IntegrationHttp.String(x, "card", "last4")}", false)).ToList(); }, token);
    }
    public async Task<PaymentProviderResult> RefundAsync(RefundRequest request, CancellationToken token = default)
    {
        if (!IsConfigured) return new(false, null, "Stripe is not configured.");
        return await ObserveAsync("Refund.Create", async ct => { using var message = Request(HttpMethod.Post, "refunds", IntegrationHttp.Form(("payment_intent", request.PaymentReference), ("amount", request.Amount is null ? null : decimal.Round(request.Amount.Value * 100m).ToString("0", CultureInfo.InvariantCulture)), ("reason", request.Reason)), request.IdempotencyKey); using var response = await Client.SendAsync(message, ct); using var json = await IntegrationHttp.JsonAsync(response, ct); return new PaymentProviderResult(true, IntegrationHttp.String(json.RootElement, "id"), null); }, token);
    }
    public async Task<DeliveryReport?> SynchronizeStatusAsync(string providerReference, CancellationToken token = default) => !IsConfigured ? null : await ObserveAsync("PaymentIntent.Get", async ct => { using var message = Request(HttpMethod.Get, $"payment_intents/{Uri.EscapeDataString(providerReference)}"); using var response = await Client.SendAsync(message, ct); using var json = await IntegrationHttp.JsonAsync(response, ct); return new DeliveryReport(providerReference, IntegrationHttp.String(json.RootElement, "status") ?? "unknown", DateTimeOffset.UtcNow); }, token);
    public bool VerifyWebhook(string payload, string signature)
    {
        var secret = Secret("WebhookSecret"); if (string.IsNullOrWhiteSpace(secret)) return false;
        var parts = signature.Split(',').Select(x => x.Split('=', 2)).Where(x => x.Length == 2).ToDictionary(x => x[0], x => x[1]);
        if (!parts.TryGetValue("t", out var timestamp) || !parts.TryGetValue("v1", out var supplied) || !long.TryParse(timestamp, out var seconds) || Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - seconds) > 300) return false;
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        try { return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(supplied)); } catch (FormatException) { return false; }
    }
}

public sealed class GoogleMapsPlatformProvider(IConfiguration configuration, IHttpClientFactory clients, IIntegrationExecutionPolicy execution, LondonVIPDbContext db, ICompanyContext company, ILogger<GoogleMapsPlatformProvider> logger)
    : ProductionProviderBase("google-maps", IntegrationCategory.Maps, configuration, clients, execution, db, company, logger), IGeocodingProvider, IRoutingProvider, IDistanceMatrixProvider, IPlacesProvider
{
    protected override bool IsConfigured => Configured("ApiKey");
    private string Url(string path, params (string Key, string Value)[] values) => $"https://maps.googleapis.com/maps/api/{path}?{string.Join('&', values.Append(("key", Secret("ApiKey")!)).Select(x => $"{Uri.EscapeDataString(x.Item1)}={Uri.EscapeDataString(x.Item2)}"))}";
    public async Task<IntegrationCoordinate?> GeocodeAsync(string address, CancellationToken token = default) => !IsConfigured ? null : await ObserveAsync("Geocode", async ct => { using var json = JsonDocument.Parse(await Client.GetStringAsync(Url("geocode/json", ("address", address)), ct)); var first = json.RootElement.GetProperty("results").EnumerateArray().FirstOrDefault(); return first.ValueKind == JsonValueKind.Undefined ? null : new IntegrationCoordinate(first.GetProperty("geometry").GetProperty("location").GetProperty("lat").GetDouble(), first.GetProperty("geometry").GetProperty("location").GetProperty("lng").GetDouble()); }, token);
    public async Task<string?> ReverseGeocodeAsync(IntegrationCoordinate point, CancellationToken token = default) => !IsConfigured ? null : await ObserveAsync("ReverseGeocode", async ct => { using var json = JsonDocument.Parse(await Client.GetStringAsync(Url("geocode/json", ("latlng", $"{point.Latitude.ToString(CultureInfo.InvariantCulture)},{point.Longitude.ToString(CultureInfo.InvariantCulture)}")), ct)); return json.RootElement.GetProperty("results").EnumerateArray().Select(x => IntegrationHttp.String(x, "formatted_address")).FirstOrDefault(); }, token);
    public async Task<IntegrationRouteResult> DirectionsAsync(IntegrationRouteRequest request, CancellationToken token = default)
    {
        if (!IsConfigured) return new(false, 0, 0, "Google Maps is not configured.");
        return await ObserveAsync("Directions", async ct => { var values = new List<(string, string)> { ("origin", Point(request.Origin)), ("destination", Point(request.Destination)), ("departure_time", "now"), ("traffic_model", "best_guess") }; if (request.Stops?.Count > 0) values.Add(("waypoints", "optimize:true|" + string.Join('|', request.Stops.Select(Point)))); using var json = JsonDocument.Parse(await Client.GetStringAsync(Url("directions/json", values.ToArray()), ct)); var route = json.RootElement.GetProperty("routes").EnumerateArray().FirstOrDefault(); if (route.ValueKind == JsonValueKind.Undefined) return new IntegrationRouteResult(false, 0, 0, IntegrationHttp.String(json.RootElement, "status")); var legs = route.GetProperty("legs").EnumerateArray().ToArray(); return new IntegrationRouteResult(true, legs.Sum(x => x.GetProperty("distance").GetProperty("value").GetDouble()) / 1609.344d, (int)Math.Ceiling(legs.Sum(x => (x.TryGetProperty("duration_in_traffic", out var traffic) ? traffic : x.GetProperty("duration")).GetProperty("value").GetDouble()) / 60d)); }, token);
    }
    public async Task<int?> EtaAsync(IntegrationRouteRequest request, CancellationToken token = default) { var route = await DirectionsAsync(request, token); return route.Success ? route.DurationMinutes : null; }
    public async Task<IReadOnlyList<IReadOnlyList<double>>> CalculateAsync(IReadOnlyList<IntegrationCoordinate> origins, IReadOnlyList<IntegrationCoordinate> destinations, CancellationToken token = default) => !IsConfigured ? [] : await ObserveAsync("DistanceMatrix", async ct => { using var json = JsonDocument.Parse(await Client.GetStringAsync(Url("distancematrix/json", ("origins", string.Join('|', origins.Select(Point))), ("destinations", string.Join('|', destinations.Select(Point))), ("departure_time", "now")), ct)); return json.RootElement.GetProperty("rows").EnumerateArray().Select(row => (IReadOnlyList<double>)row.GetProperty("elements").EnumerateArray().Select(x => x.TryGetProperty("distance", out var distance) ? distance.GetProperty("value").GetDouble() / 1609.344d : double.NaN).ToList()).ToList(); }, token);
    public async Task<IReadOnlyList<string>> SearchAsync(string query, CancellationToken token = default) => !IsConfigured ? [] : await ObserveAsync("Places.Autocomplete", async ct => { using var json = JsonDocument.Parse(await Client.GetStringAsync(Url("place/autocomplete/json", ("input", query), ("components", "country:gb")), ct)); return json.RootElement.GetProperty("predictions").EnumerateArray().Select(x => IntegrationHttp.String(x, "description")!).Where(x => x is not null).ToList(); }, token);
    public async Task<byte[]?> StaticMapAsync(IntegrationCoordinate centre, CancellationToken token = default) => !IsConfigured ? null : await ObserveAsync("StaticMap", ct => Client.GetByteArrayAsync(Url("staticmap", ("center", Point(centre)), ("zoom", "14"), ("size", "800x450"), ("markers", Point(centre))), ct), token);
    private static string Point(IntegrationCoordinate point) => $"{point.Latitude.ToString(CultureInfo.InvariantCulture)},{point.Longitude.ToString(CultureInfo.InvariantCulture)}";
}
