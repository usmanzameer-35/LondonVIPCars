using LondonVIP.Shared.Integrations;

namespace LondonVIP.Shared.Models;

public sealed class IntegrationWebhookDelivery
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public string ProviderKey { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public WebhookDirection Direction { get; set; }
    public WebhookDeliveryState Status { get; set; }
    public string DeliveryId { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string? Signature { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? LastError { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}

public sealed class IntegrationProviderMetric
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public string ProviderKey { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public bool Success { get; set; }
    public long LatencyMilliseconds { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorCode { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class IntegrationResourceReference
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public string ProviderKey { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public Guid LocalResourceId { get; set; }
    public string ProviderResourceId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class IntegrationCommunicationLog
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public Guid? BookingId { get; set; }
    public Booking? Booking { get; set; }
    public string ProviderKey { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string ProviderReference { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
