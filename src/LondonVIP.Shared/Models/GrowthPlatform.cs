namespace LondonVIP.Shared.Models;

public enum DiscountKind { Percentage, Fixed }
public enum GrowthStatus { Draft, Active, Paused, Completed, Cancelled }
public enum ReferralStatus { Pending, Qualified, Rewarded, Rejected }
public enum LoyaltyTier { Bronze, Silver, Gold, Vip }
public enum ContentStatus { Draft, Scheduled, Published, Archived }
public enum MarketingChannel { Email, Sms, WhatsApp, Push, LinkedIn, Facebook, Instagram, GoogleBusiness, YouTube, TikTok, X }

public sealed class Promotion
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public Company Company { get; set; } = null!;
    public string Code { get; set; } = string.Empty; public string Name { get; set; } = string.Empty; public DiscountKind Kind { get; set; }
    public decimal Value { get; set; } public decimal? MaximumDiscount { get; set; } public decimal? MinimumSpend { get; set; }
    public Guid? AirportId { get; set; } public Airport? Airport { get; set; } public string? PickupPattern { get; set; } public string? DestinationPattern { get; set; }
    public bool FirstBookingOnly { get; set; } public bool ReturningCustomersOnly { get; set; } public bool CorporateOnly { get; set; }
    public bool IsActive { get; set; } = true; public bool AllowStacking { get; set; } public int? UsageLimit { get; set; } public int? PerCustomerLimit { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; } public DateTimeOffset? EffectiveTo { get; set; } public DateTimeOffset CreatedAt { get; set; }
    public ICollection<PromotionRedemption> Redemptions { get; set; } = [];
}
public sealed class PromotionRedemption
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public Guid PromotionId { get; set; } public Promotion Promotion { get; set; } = null!;
    public Guid? CustomerId { get; set; } public Guid? BookingId { get; set; } public Guid? QuotationId { get; set; } public decimal DiscountAmount { get; set; } public DateTimeOffset RedeemedAt { get; set; }
}
public sealed class Referral
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public string Code { get; set; } = string.Empty; public string ReferrerType { get; set; } = string.Empty;
    public Guid ReferrerId { get; set; } public Guid? ReferredCustomerId { get; set; } public ReferralStatus Status { get; set; } public decimal RewardAmount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } public DateTimeOffset? QualifiedAt { get; set; } public DateTimeOffset? RewardedAt { get; set; }
}
public sealed class LoyaltyAccount
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public Guid CustomerId { get; set; } public Customer Customer { get; set; } = null!;
    public int PointsBalance { get; set; } public int LifetimePoints { get; set; } public LoyaltyTier Tier { get; set; } public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<LoyaltyTransaction> Transactions { get; set; } = [];
}
public sealed class LoyaltyTransaction
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public Guid LoyaltyAccountId { get; set; } public LoyaltyAccount LoyaltyAccount { get; set; } = null!;
    public int Points { get; set; } public string Reason { get; set; } = string.Empty; public Guid? BookingId { get; set; } public string? VoucherCode { get; set; } public DateTimeOffset CreatedAt { get; set; }
}
public sealed class NewsletterSubscriber
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public string Email { get; set; } = string.Empty; public string? Name { get; set; }
    public string Lists { get; set; } = string.Empty; public string Segments { get; set; } = string.Empty; public bool IsConfirmed { get; set; } public string ConfirmationTokenHash { get; set; } = string.Empty;
    public DateTimeOffset SubscribedAt { get; set; } public DateTimeOffset? ConfirmationExpiresAt { get; set; } public DateTimeOffset? ConfirmedAt { get; set; } public DateTimeOffset? UnsubscribedAt { get; set; } public int ConfirmationSendCount { get; set; }
}
public sealed class MarketingCampaign
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public string Name { get; set; } = string.Empty; public MarketingChannel Channel { get; set; }
    public GrowthStatus Status { get; set; } public string AudienceDefinition { get; set; } = string.Empty; public string TemplateName { get; set; } = string.Empty;
    public DateTimeOffset? ScheduledAt { get; set; } public int SentCount { get; set; } public int DeliveredCount { get; set; } public int OpenCount { get; set; }
    public int ClickCount { get; set; } public int ConversionCount { get; set; } public decimal Cost { get; set; } public decimal Revenue { get; set; } public string? RecurrenceRule { get; set; } public DateTimeOffset CreatedAt { get; set; } public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<CampaignDelivery> Deliveries { get; set; } = [];
}
public sealed class CmsPage
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public string Slug { get; set; } = string.Empty; public string Title { get; set; } = string.Empty;
    public string PageType { get; set; } = string.Empty; public string ContentJson { get; set; } = string.Empty; public string MetaTitle { get; set; } = string.Empty;
    public string MetaDescription { get; set; } = string.Empty; public string? CanonicalUrl { get; set; } public ContentStatus Status { get; set; }
    public DateTimeOffset? PublishAt { get; set; } public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class BlogArticle
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public string Slug { get; set; } = string.Empty; public string Title { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty; public string Body { get; set; } = string.Empty; public string Author { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; public string Tags { get; set; } = string.Empty; public string MetaTitle { get; set; } = string.Empty;
    public string MetaDescription { get; set; } = string.Empty; public ContentStatus Status { get; set; } public DateTimeOffset? PublishAt { get; set; } public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class SeoRedirect
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public string SourcePath { get; set; } = string.Empty; public string DestinationUrl { get; set; } = string.Empty;
    public int StatusCode { get; set; } = 301; public bool IsActive { get; set; } = true; public DateTimeOffset CreatedAt { get; set; }
}
public sealed class SocialPost
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public MarketingChannel Channel { get; set; } public string AccountKey { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty; public string? MediaAssetIds { get; set; } public GrowthStatus Status { get; set; } public DateTimeOffset? ScheduledAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; } public string? ProviderReference { get; set; } public int EngagementCount { get; set; } public PublishStatus PublishStatus { get; set; } public int AttemptCount { get; set; } public DateTimeOffset? NextAttemptAt { get; set; } public string? LastError { get; set; } public DateTimeOffset CreatedAt { get; set; }
}
public sealed class MediaAsset
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public string Folder { get; set; } = string.Empty; public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty; public long SizeBytes { get; set; } public string StoragePath { get; set; } = string.Empty; public string Tags { get; set; } = string.Empty;
    public int Version { get; set; } = 1; public Guid? PreviousVersionId { get; set; } public string Sha256 { get; set; } = string.Empty; public int? Width { get; set; } public int? Height { get; set; } public double? DurationSeconds { get; set; } public string? ThumbnailPath { get; set; } public string? WebPPath { get; set; } public string? CdnUrl { get; set; } public bool IsArchived { get; set; } public DateTimeOffset CreatedAt { get; set; }
}
public sealed class LeadCapture
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public string Type { get; set; } = string.Empty; public string Source { get; set; } = string.Empty;
    public string? Campaign { get; set; } public string? Name { get; set; } public string? Email { get; set; } public string? Phone { get; set; } public string PayloadJson { get; set; } = string.Empty;
    public Guid? CrmLeadId { get; set; } public DateTimeOffset CreatedAt { get; set; }
}
