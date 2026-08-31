using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Shared.Integrations;
using LondonVIP.Shared.Tenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LondonVIP.Infrastructure.Integrations;

public abstract class TwilioCommunicationProvider(
    string key, IConfiguration configuration, IHttpClientFactory clients, IIntegrationExecutionPolicy execution,
    LondonVIPDbContext db, ICompanyContext company, ILogger logger)
    : ProductionProviderBase(key, IntegrationCategory.Communications, configuration, clients, execution, db, company, logger), ICommunicationProvider
{
    protected override bool IsConfigured => Configured("AccountSid", "AuthToken", "From");
    public IReadOnlyCollection<string> Templates => Configuration.GetSection($"Integrations:{Key}:Templates").GetChildren().Select(x => x.Key).ToArray();
    protected virtual string Recipient(string value) => value;
    public async Task<CommunicationResult> SendAsync(CommunicationRequest request, CancellationToken token = default)
    {
        if (!IsConfigured) return new(false, null, $"{Key} is not configured.");
        return await ObserveAsync("Message.Send", async ct =>
        {
            var body = Render(request.Template, request.Data);
            using var message = new HttpRequestMessage(HttpMethod.Post, $"https://api.twilio.com/2010-04-01/Accounts/{Secret("AccountSid")}/Messages.json") { Content = IntegrationHttp.Form(("From", Recipient(Secret("From")!)), ("To", Recipient(request.Recipient)), ("Body", body), ("StatusCallback", Secret("StatusCallbackUrl"))) };
            message.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Secret("AccountSid")}:{Secret("AuthToken")}")));
            using var response = await Client.SendAsync(message, ct); using var json = await IntegrationHttp.JsonAsync(response, ct);
            return new CommunicationResult(true, IntegrationHttp.String(json.RootElement, "sid"), null);
        }, token);
    }
    public Task<CommunicationResult> RetryAsync(string providerReference, CancellationToken cancellationToken = default) => Task.FromResult(new CommunicationResult(false, providerReference, "Retry requires the original persisted notification payload."));
    public async Task<DeliveryReport?> GetStatusAsync(string providerReference, CancellationToken token = default) => !IsConfigured ? null : await ObserveAsync("Message.Status", async ct => { using var message = new HttpRequestMessage(HttpMethod.Get, $"https://api.twilio.com/2010-04-01/Accounts/{Secret("AccountSid")}/Messages/{Uri.EscapeDataString(providerReference)}.json"); message.Headers.Authorization = new("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Secret("AccountSid")}:{Secret("AuthToken")}"))); using var response = await Client.SendAsync(message, ct); using var json = await IntegrationHttp.JsonAsync(response, ct); return new DeliveryReport(providerReference, IntegrationHttp.String(json.RootElement, "status") ?? "unknown", DateTimeOffset.UtcNow); }, token);
    protected string Render(string template, IReadOnlyDictionary<string, string> data) { var body = Configuration[$"Integrations:{Key}:Templates:{template}"] ?? template; foreach (var item in data) body = body.Replace($"{{{{{item.Key}}}}}", item.Value, StringComparison.Ordinal); return body; }
}

public sealed class TwilioSmsProvider(IConfiguration c, IHttpClientFactory h, IIntegrationExecutionPolicy e, LondonVIPDbContext d, ICompanyContext t, ILogger<TwilioSmsProvider> l) : TwilioCommunicationProvider("twilio-sms", c, h, e, d, t, l);
public sealed class TwilioWhatsAppProvider(IConfiguration c, IHttpClientFactory h, IIntegrationExecutionPolicy e, LondonVIPDbContext d, ICompanyContext t, ILogger<TwilioWhatsAppProvider> l) : TwilioCommunicationProvider("twilio-whatsapp", c, h, e, d, t, l), IWhatsAppProvider
{ protected override string Recipient(string value) => value.StartsWith("whatsapp:", StringComparison.OrdinalIgnoreCase) ? value : $"whatsapp:{value}"; }

public sealed class SendGridEmailProvider(IConfiguration configuration, IHttpClientFactory clients, IIntegrationExecutionPolicy execution, LondonVIPDbContext db, ICompanyContext company, ILogger<SendGridEmailProvider> logger)
    : ProductionProviderBase("sendgrid", IntegrationCategory.Communications, configuration, clients, execution, db, company, logger), ICommunicationProvider
{
    protected override bool IsConfigured => Configured("ApiKey", "FromEmail");
    public IReadOnlyCollection<string> Templates => Configuration.GetSection("Integrations:sendgrid:Templates").GetChildren().Select(x => x.Key).ToArray();
    public async Task<CommunicationResult> SendAsync(CommunicationRequest request, CancellationToken token = default)
    {
        if (!IsConfigured) return new(false, null, "SendGrid is not configured.");
        return await ObserveAsync("Email.Send", async ct => { var templateId = Configuration[$"Integrations:sendgrid:Templates:{request.Template}"]; var personal = new { to = new[] { new { email = request.Recipient } }, dynamic_template_data = request.Data }; object payload = string.IsNullOrWhiteSpace(templateId) ? new { personalizations = new[] { personal }, from = new { email = Secret("FromEmail"), name = Secret("FromName") ?? "London VIP Cars" }, subject = request.Template, content = new[] { new { type = "text/plain", value = string.Join(Environment.NewLine, request.Data.Select(x => $"{x.Key}: {x.Value}")) } } } : new { personalizations = new[] { personal }, from = new { email = Secret("FromEmail"), name = Secret("FromName") ?? "London VIP Cars" }, template_id = templateId }; using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send") { Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json") }; message.Headers.Authorization = new("Bearer", Secret("ApiKey")); using var response = await Client.SendAsync(message, ct); if (!response.IsSuccessStatusCode) throw new HttpRequestException($"SendGrid returned {(int)response.StatusCode}."); var reference = response.Headers.TryGetValues("X-Message-Id", out var values) ? values.FirstOrDefault() : request.CorrelationId; return new CommunicationResult(true, reference, null); }, token);
    }
    public Task<CommunicationResult> RetryAsync(string providerReference, CancellationToken cancellationToken = default) => Task.FromResult(new CommunicationResult(false, providerReference, "Retry requires the original persisted notification payload."));
    public Task<DeliveryReport?> GetStatusAsync(string providerReference, CancellationToken cancellationToken = default) => Task.FromResult<DeliveryReport?>(null);
}

public sealed class TwilioVoiceProvider(IConfiguration configuration, IHttpClientFactory clients, IIntegrationExecutionPolicy execution, LondonVIPDbContext db, ICompanyContext company, ILogger<TwilioVoiceProvider> logger)
    : TwilioCommunicationProvider("twilio-voice", configuration, clients, execution, db, company, logger), IVoiceCallProvider
{
    public async Task<VoiceCallResult> StartCallAsync(VoiceCallRequest request, CancellationToken token = default)
    {
        if (!Configured("AccountSid", "AuthToken", "From")) return new(false, null, "Twilio Voice is not configured.");
        return await ObserveAsync("Call.Start", async ct => { using var message = new HttpRequestMessage(HttpMethod.Post, $"https://api.twilio.com/2010-04-01/Accounts/{Secret("AccountSid")}/Calls.json") { Content = IntegrationHttp.Form(("From", request.From), ("To", request.To), ("Url", request.CallbackUrl.ToString()), ("StatusCallback", Secret("StatusCallbackUrl")), ("StatusCallbackEvent", "initiated ringing answered completed")) }; message.Headers.Authorization = new("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Secret("AccountSid")}:{Secret("AuthToken")}"))); using var response = await Client.SendAsync(message, ct); using var json = await IntegrationHttp.JsonAsync(response, ct); var sid = IntegrationHttp.String(json.RootElement, "sid"); if (sid is not null) { Db.IntegrationCommunicationLogs.Add(new() { Id = Guid.NewGuid(), CompanyId = CompanyId, BookingId = request.BookingId, ProviderKey = Key, Channel = "Voice", Recipient = request.To, ProviderReference = sid, Status = "queued", CorrelationId = request.CorrelationId, CreatedAt = DateTimeOffset.UtcNow }); await Db.SaveChangesAsync(ct); } return new VoiceCallResult(true, sid, null); }, token);
    }
}

public sealed class AviationStackFlightProvider(IConfiguration configuration, IHttpClientFactory clients, IIntegrationExecutionPolicy execution, LondonVIPDbContext db, ICompanyContext company, ILogger<AviationStackFlightProvider> logger)
    : ProductionProviderBase("aviationstack", IntegrationCategory.Flights, configuration, clients, execution, db, company, logger), IFlightMonitoringProvider
{
    protected override bool IsConfigured => Configured("ApiKey");
    public async Task<FlightDataResult?> LookupAsync(string flightNumber, DateOnly date, CancellationToken token = default) => !IsConfigured ? null : (await QueryAsync($"flight_iata={Uri.EscapeDataString(flightNumber)}&flight_date={date:yyyy-MM-dd}", token)).FirstOrDefault();
    public async Task<IReadOnlyDictionary<string, string>> GetAirportMetadataAsync(string airportCode, CancellationToken token = default)
    {
        if (!IsConfigured) return new Dictionary<string, string>();
        return await ObserveAsync("Airport.Lookup", async ct => { using var json = JsonDocument.Parse(await Client.GetStringAsync($"https://api.aviationstack.com/v1/airports?access_key={Uri.EscapeDataString(Secret("ApiKey")!)}&iata_code={Uri.EscapeDataString(airportCode)}", ct)); var item = json.RootElement.GetProperty("data").EnumerateArray().FirstOrDefault(); return (IReadOnlyDictionary<string, string>)(item.ValueKind == JsonValueKind.Undefined ? new Dictionary<string, string>() : new Dictionary<string, string> { ["iata"] = IntegrationHttp.String(item, "iata_code") ?? airportCode, ["name"] = IntegrationHttp.String(item, "airport_name") ?? "", ["timezone"] = IntegrationHttp.String(item, "timezone") ?? "" }); }, token);
    }
    public Task<IReadOnlyList<FlightDataResult>> MonitorArrivalsAsync(string airportCode, DateTimeOffset from, DateTimeOffset to, CancellationToken token = default) => QueryAsync($"arr_iata={Uri.EscapeDataString(airportCode)}", token);
    public Task<IReadOnlyList<FlightDataResult>> MonitorDeparturesAsync(string airportCode, DateTimeOffset from, DateTimeOffset to, CancellationToken token = default) => QueryAsync($"dep_iata={Uri.EscapeDataString(airportCode)}", token);
    private async Task<IReadOnlyList<FlightDataResult>> QueryAsync(string query, CancellationToken token)
    {
        if (!IsConfigured) return [];
        return await ObserveAsync("Flight.Lookup", async ct => { using var json = JsonDocument.Parse(await Client.GetStringAsync($"https://api.aviationstack.com/v1/flights?access_key={Uri.EscapeDataString(Secret("ApiKey")!)}&{query}", ct)); return json.RootElement.GetProperty("data").EnumerateArray().Select(x => { var status = IntegrationHttp.String(x, "flight_status") ?? "unknown"; var flight = IntegrationHttp.String(x, "flight", "iata") ?? IntegrationHttp.String(x, "flight", "number") ?? ""; var gate = IntegrationHttp.String(x, "arrival", "gate") ?? IntegrationHttp.String(x, "departure", "gate"); var delay = int.TryParse(IntegrationHttp.String(x, "arrival", "delay"), out var minutes) ? minutes : 0; var predicted = DateTimeOffset.TryParse(IntegrationHttp.String(x, "arrival", "estimated"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var eta) ? eta : (DateTimeOffset?)null; return new FlightDataResult(flight, status, gate, delay, predicted, status.Equals("cancelled", StringComparison.OrdinalIgnoreCase)); }).ToList(); }, token);
    }
}
