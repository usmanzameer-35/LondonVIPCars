namespace LondonVIP.Shared.Models;

public enum ProviderConnectionStatus { Disabled, PendingAuthorization, Connected, TokenExpired, Unhealthy, Disconnected }
public enum PublishStatus { Draft, Queued, Publishing, Published, Failed, Cancelled, Archived }
public enum CampaignDeliveryStatus { Pending, Sending, Delivered, Failed, Cancelled, DeadLettered }
public enum AnalyticsEventType { PageView, SessionStarted, QuoteStarted, QuoteCompleted, BookingStarted, BookingCompleted, Conversion, Goal }

public sealed class SocialProviderConnection
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public MarketingChannel Provider { get; set; } public string AccountKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty; public ProviderConnectionStatus Status { get; set; } public string ProtectedAccessToken { get; set; } = string.Empty;
    public string? ProtectedRefreshToken { get; set; } public DateTimeOffset? TokenExpiresAt { get; set; } public string Scopes { get; set; } = string.Empty;
    public string? LastError { get; set; } public DateTimeOffset? LastHealthCheckAt { get; set; } public DateTimeOffset CreatedAt { get; set; } public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class OAuthAuthorizationState
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public MarketingChannel Provider { get; set; } public string StateHash { get; set; } = string.Empty;
    public string CodeVerifierProtected { get; set; } = string.Empty; public string RedirectUri { get; set; } = string.Empty; public DateTimeOffset ExpiresAt { get; set; } public DateTimeOffset? ConsumedAt { get; set; }
}
public sealed class CampaignDelivery
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public Guid MarketingCampaignId { get; set; } public MarketingCampaign MarketingCampaign { get; set; } = null!;
    public string Recipient { get; set; } = string.Empty; public string RecipientType { get; set; } = string.Empty; public CampaignDeliveryStatus Status { get; set; }
    public int AttemptCount { get; set; } public DateTimeOffset? NextAttemptAt { get; set; } public string? ProviderReference { get; set; } public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; } public DateTimeOffset? DeliveredAt { get; set; }
}
public sealed class AnalyticsSession
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public string AnonymousIdHash { get; set; } = string.Empty; public string SessionKeyHash { get; set; } = string.Empty;
    public string? Source { get; set; } public string? Medium { get; set; } public string? Campaign { get; set; } public string? ReferrerHost { get; set; }
    public DateTimeOffset StartedAt { get; set; } public DateTimeOffset LastSeenAt { get; set; } public ICollection<AnalyticsEvent> Events { get; set; } = [];
}
public sealed class AnalyticsEvent
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public Guid AnalyticsSessionId { get; set; } public AnalyticsSession AnalyticsSession { get; set; } = null!;
    public AnalyticsEventType Type { get; set; } public string Path { get; set; } = string.Empty; public string? Goal { get; set; } public decimal? Value { get; set; }
    public Guid? QuotationId { get; set; } public Guid? BookingId { get; set; } public Guid? MarketingCampaignId { get; set; } public DateTimeOffset OccurredAt { get; set; }
}
public sealed class AiMarketingGeneration
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public string GenerationType { get; set; } = string.Empty; public string Prompt { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty; public string ProviderKey { get; set; } = string.Empty; public string Model { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; public int InputTokens { get; set; } public int OutputTokens { get; set; } public DateTimeOffset CreatedAt { get; set; }
}
public sealed class ContentTaxonomy
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public string Type { get; set; } = string.Empty; public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty; public string? Description { get; set; } public bool IsActive { get; set; } = true;
}
