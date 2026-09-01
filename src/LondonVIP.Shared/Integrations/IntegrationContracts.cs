namespace LondonVIP.Shared.Integrations;

public enum IntegrationCategory { Payments, Maps, Communications, Flights, Storage, Pdf, Secrets, Webhooks }
public enum IntegrationHealthState { Healthy, Degraded, Unavailable, NotConfigured }
public enum WebhookDirection { Incoming, Outgoing }
public enum WebhookDeliveryState { Pending, Delivered, Failed, DeadLettered }

public sealed record ProviderDescriptor(string Key, string DisplayName, IntegrationCategory Category, bool IsConfigured, bool IsEnabled);
public sealed record ProviderHealthDto(string Key, IntegrationCategory Category, IntegrationHealthState State, long LatencyMilliseconds, int FailureCount, int RetryCount, string Message, DateTimeOffset CheckedAt);
public sealed record IntegrationDashboardDto(IReadOnlyList<ProviderDescriptor> Providers, IReadOnlyList<ProviderHealthDto> Health, int PendingWebhooks, int FailedWebhooks, bool SecretsAvailable);
public sealed record ConnectionTestRequest(string ProviderKey);
public sealed record ConnectionTestResult(string ProviderKey, bool Success, string Message, long LatencyMilliseconds);
public sealed record WebhookTestRequest(string ProviderKey, string EventType, string Payload, string? Signature = null, string? DeliveryId = null);
public sealed record WebhookResult(bool Accepted, bool Duplicate, string Message);
public sealed record IntegrationConfigurationDto(string ProviderKey, IntegrationCategory Category, bool Enabled, IReadOnlyDictionary<string, string> Settings, bool HasCredentials);

public interface IIntegrationProvider
{
    string Key { get; }
    IntegrationCategory Category { get; }
    Task<ProviderHealthDto> CheckHealthAsync(CancellationToken cancellationToken = default);
}

public interface IProviderRegistry
{
    IReadOnlyList<ProviderDescriptor> Describe();
    IIntegrationProvider? Find(string key);
}
public interface IIntegrationExecutionPolicy
{
    Task<T> ExecuteAsync<T>(string providerKey, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
}

public sealed record PaymentProviderRequest(decimal Amount, string CurrencyCode, string Reference, string? PaymentMethodToken);
public sealed record PaymentProviderResult(bool Success, string? ProviderReference, string? Error);
public interface IIntegrationPaymentProvider : IIntegrationProvider { Task<PaymentProviderResult> AuthorizeAsync(PaymentProviderRequest request, CancellationToken cancellationToken = default); }
public sealed record ProviderCustomerRequest(Guid CustomerId, string Email, string Name, string? Phone);
public sealed record ProviderCustomerResult(bool Success, string? ProviderCustomerId, string? Error);
public sealed record SavedPaymentMethodDto(string Id, string Type, string DisplayName, bool IsDefault);
public sealed record RefundRequest(string PaymentReference, decimal? Amount, string IdempotencyKey, string? Reason = null);
public interface IPaymentLifecycleProvider : IIntegrationPaymentProvider
{
    Task<ProviderCustomerResult> CreateCustomerAsync(ProviderCustomerRequest request, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SavedPaymentMethodDto>> GetSavedPaymentMethodsAsync(string providerCustomerId, CancellationToken cancellationToken = default);
    Task<PaymentProviderResult> RefundAsync(RefundRequest request, CancellationToken cancellationToken = default);
    Task<DeliveryReport?> SynchronizeStatusAsync(string providerReference, CancellationToken cancellationToken = default);
    bool VerifyWebhook(string payload, string signature);
}

public sealed record IntegrationCoordinate(double Latitude, double Longitude);
public sealed record IntegrationRouteRequest(IntegrationCoordinate Origin, IntegrationCoordinate Destination, IReadOnlyList<IntegrationCoordinate>? Stops = null);
public sealed record IntegrationRouteResult(bool Success, double DistanceMiles, int DurationMinutes, string? Error = null);
public interface IGeocodingProvider : IIntegrationProvider { Task<IntegrationCoordinate?> GeocodeAsync(string address, CancellationToken cancellationToken = default); Task<string?> ReverseGeocodeAsync(IntegrationCoordinate coordinate, CancellationToken cancellationToken = default); }
public interface IRoutingProvider : IIntegrationProvider { Task<IntegrationRouteResult> DirectionsAsync(IntegrationRouteRequest request, CancellationToken cancellationToken = default); Task<int?> EtaAsync(IntegrationRouteRequest request, CancellationToken cancellationToken = default); }
public interface IDistanceMatrixProvider : IIntegrationProvider { Task<IReadOnlyList<IReadOnlyList<double>>> CalculateAsync(IReadOnlyList<IntegrationCoordinate> origins, IReadOnlyList<IntegrationCoordinate> destinations, CancellationToken cancellationToken = default); }
public interface IPlacesProvider : IIntegrationProvider { Task<IReadOnlyList<string>> SearchAsync(string query, CancellationToken cancellationToken = default); Task<byte[]?> StaticMapAsync(IntegrationCoordinate centre, CancellationToken cancellationToken = default); }

public sealed record CommunicationRequest(string Recipient, string Template, IReadOnlyDictionary<string, string> Data, string CorrelationId);
public sealed record CommunicationResult(bool Accepted, string? ProviderReference, string? Error);
public sealed record DeliveryReport(string ProviderReference, string Status, DateTimeOffset UpdatedAt);
public interface ICommunicationProvider : IIntegrationProvider
{
    Task<CommunicationResult> SendAsync(CommunicationRequest request, CancellationToken cancellationToken = default);
    Task<CommunicationResult> RetryAsync(string providerReference, CancellationToken cancellationToken = default);
    Task<DeliveryReport?> GetStatusAsync(string providerReference, CancellationToken cancellationToken = default);
    IReadOnlyCollection<string> Templates { get; }
}
public interface IWhatsAppProvider : ICommunicationProvider;
public interface IPushNotificationProvider : ICommunicationProvider;
public interface IVoiceCallingProvider : ICommunicationProvider;
public sealed record VoiceCallRequest(string From, string To, Uri CallbackUrl, Guid? BookingId, string CorrelationId);
public sealed record VoiceCallResult(bool Accepted, string? ProviderReference, string? Error);
public interface IVoiceCallProvider : IVoiceCallingProvider { Task<VoiceCallResult> StartCallAsync(VoiceCallRequest request, CancellationToken cancellationToken = default); }

public sealed record FlightDataResult(string FlightNumber, string Status, string? Gate, int DelayMinutes, DateTimeOffset? PredictedArrival, bool IsCancelled);
public interface IFlightDataProvider : IIntegrationProvider { Task<FlightDataResult?> LookupAsync(string flightNumber, DateOnly date, CancellationToken cancellationToken = default); Task<IReadOnlyDictionary<string, string>> GetAirportMetadataAsync(string airportCode, CancellationToken cancellationToken = default); }
public interface IFlightMonitoringProvider : IFlightDataProvider { Task<IReadOnlyList<FlightDataResult>> MonitorArrivalsAsync(string airportCode, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default); Task<IReadOnlyList<FlightDataResult>> MonitorDeparturesAsync(string airportCode, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default); }

public interface IFileStorageProvider : IIntegrationProvider { Task<string> SaveAsync(string path, Stream content, string contentType, CancellationToken cancellationToken = default); Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken = default); Task DeleteAsync(string path, CancellationToken cancellationToken = default); }
public enum PdfDocumentType { Invoice, Receipt, BookingConfirmation, CorporateStatement, DriverSummary, BalanceSheet, ProfitAndLoss, TrialBalance, CashFlow, VatReport, CustomerStatement, SupplierStatement, DriverSettlement, Journal, CreditNote }
public interface IPdfGenerationProvider : IIntegrationProvider { Task<byte[]> GenerateAsync(PdfDocumentType documentType, object model, CancellationToken cancellationToken = default); }
public interface ISecretProvider { Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default); Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default); }

public interface IWebhookSignatureValidator { bool Validate(string payload, string signature, string secret); }
public interface IWebhookEngine
{
    Task<WebhookResult> ReceiveAsync(string providerKey, string eventType, string payload, string? signature, string deliveryId, CancellationToken cancellationToken = default);
    Task<WebhookResult> SendAsync(string providerKey, string eventType, string payload, CancellationToken cancellationToken = default);
    int PendingCount { get; }
    int FailedCount { get; }
}
public sealed record WebhookDeliveryDto(Guid Id, string ProviderKey, string EventType, WebhookDirection Direction, WebhookDeliveryState Status, int AttemptCount, DateTimeOffset CreatedAt, DateTimeOffset? NextAttemptAt, string? LastError);
public interface IWebhookAdministrationService { Task<IReadOnlyList<WebhookDeliveryDto>> ListAsync(WebhookDeliveryState? status, CancellationToken cancellationToken = default); Task<bool> RetryAsync(Guid id, CancellationToken cancellationToken = default); }
