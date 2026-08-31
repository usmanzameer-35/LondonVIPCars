using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.Growth;

public sealed record PromotionRequest(string Code, string Name, DiscountKind Kind, decimal Value, decimal? MaximumDiscount, decimal? MinimumSpend,
    Guid? AirportId, string? PickupPattern, string? DestinationPattern, bool FirstBookingOnly, bool ReturningCustomersOnly, bool CorporateOnly,
    bool AllowStacking, int? UsageLimit, int? PerCustomerLimit, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, bool IsActive = true);
public sealed record PromotionContext(string Code, decimal Fare, Guid? CustomerId, Guid? CorporateAccountId, Guid? AirportId, string Pickup, string Destination, int PriorBookings);
public sealed record PromotionValidationResult(bool IsValid, string Message, Guid? PromotionId = null, decimal Discount = 0);
public sealed record ReferralRequest(string ReferrerType, Guid ReferrerId, decimal RewardAmount);
public sealed record ReferralDto(Guid Id, string Code, string Link, string ReferrerType, ReferralStatus Status, decimal RewardAmount, DateTimeOffset CreatedAt);
public sealed record LoyaltySummaryDto(Guid CustomerId, int PointsBalance, int LifetimePoints, LoyaltyTier Tier, IReadOnlyList<LoyaltyTransactionDto> History);
public sealed record LoyaltyTransactionDto(int Points, string Reason, string? VoucherCode, DateTimeOffset CreatedAt);
public sealed record LoyaltyChangeRequest(Guid CustomerId, int Points, string Reason, Guid? BookingId);
public sealed record NewsletterRequest(string Email, string? Name, string? Lists, string? Segments);
public sealed record CampaignRequest(string Name, MarketingChannel Channel, string AudienceDefinition, string TemplateName, DateTimeOffset? ScheduledAt);
public sealed record ContentRequest(string Slug, string Title, string PageType, string ContentJson, string MetaTitle, string MetaDescription, string? CanonicalUrl, ContentStatus Status, DateTimeOffset? PublishAt);
public sealed record BlogRequest(string Slug, string Title, string Excerpt, string Body, string Author, string Category, string Tags, string MetaTitle, string MetaDescription, ContentStatus Status, DateTimeOffset? PublishAt);
public sealed record LeadCaptureRequest(string Type, string Source, string? Campaign, string? Name, string? Email, string? Phone, string? Message);
public sealed record GrowthDashboardDto(int ActivePromotions, int PendingReferrals, int LoyaltyMembers, int ActiveCampaigns, int Subscribers, int PublishedPages,
    int PublishedArticles, int ScheduledSocialPosts, int MediaAssets, int CapturedLeads, decimal CampaignRevenue, decimal PromotionDiscounts);

public interface IPromotionEngine
{
    Task<PromotionValidationResult> ValidateAsync(PromotionContext context, CancellationToken token = default);
}
public interface IReferralService
{
    Task<ReferralDto> CreateAsync(ReferralRequest request, CancellationToken token = default);
    Task<bool> QualifyAsync(Guid id, Guid customerId, CancellationToken token = default);
}
public interface ILoyaltyService
{
    Task<LoyaltySummaryDto?> GetAsync(Guid customerId, CancellationToken token = default);
    Task<LoyaltySummaryDto> ChangePointsAsync(LoyaltyChangeRequest request, CancellationToken token = default);
}
