using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.Growth;

public sealed record ProviderConfigurationStatus(MarketingChannel Provider,bool Enabled,bool Configured,bool Connected,string Status,string? Error);
public sealed record OAuthStartResult(bool Success,string? AuthorizationUrl,string Message);
public sealed record OAuthCallbackRequest(string Code,string State,string RedirectUri);
public sealed record SocialPublishRequest(Guid SocialPostId);
public sealed record ProviderOperationResult(bool Success,bool NotConfigured,string Message,string? ProviderReference=null);
public sealed record OAuthTokenResult(bool Success,bool NotConfigured,string Message,string? AccessToken=null,string? RefreshToken=null,DateTimeOffset? ExpiresAt=null,string? AccountKey=null,string? DisplayName=null,string? Scopes=null);
public sealed record CampaignCommandRequest(string Action);
public sealed record BulkGrowthRequest(IReadOnlyList<Guid> Ids,string Action);
public sealed record AnalyticsTrackRequest(string AnonymousId,string SessionKey,AnalyticsEventType Type,string Path,string? Source,string? Medium,string? Campaign,string? ReferrerHost,string? Goal,decimal? Value,Guid? QuotationId,Guid? BookingId);
public sealed record GrowthReportDto(decimal CampaignRoi,decimal QuoteConversionRate,decimal BookingConversionRate,IReadOnlyDictionary<string,int> LeadSources,IReadOnlyDictionary<string,int> SocialEngagement,int NewsletterDelivered,int NewsletterOpened,int Reviews,int Referrals,int LoyaltyMembers);
public sealed record AiMarketingRequest(string GenerationType,string Brief,string Tone,string? Audience,string? Keywords);
public sealed record AiMarketingResult(bool Success,bool NotConfigured,string Message,string? Content=null,IReadOnlyList<string>? Suggestions=null);
public sealed record MediaUploadRequest(string Folder,string FileName,string ContentType,string Tags,Stream Content);

public interface ISocialProvider
{
    MarketingChannel Provider { get; }
    bool IsConfigured { get; }
    string BuildAuthorizationUrl(string state,string codeChallenge,string redirectUri);
    Task<OAuthTokenResult> ExchangeCodeAsync(string code,string codeVerifier,string redirectUri,CancellationToken token=default);
    Task<ProviderOperationResult> RefreshAsync(SocialProviderConnection connection,CancellationToken token=default);
    Task<ProviderOperationResult> PublishAsync(SocialProviderConnection connection,SocialPost post,IReadOnlyList<MediaAsset> media,CancellationToken token=default);
    Task<bool> ValidateWebhookAsync(string payload,string? signature,CancellationToken token=default);
}
public interface ISocialProviderRegistry { IReadOnlyList<ISocialProvider> All { get; } ISocialProvider? Find(MarketingChannel provider); }
public interface ISocialOperationsService
{
    Task<IReadOnlyList<ProviderConfigurationStatus>> StatusAsync(CancellationToken token=default);
    Task<OAuthStartResult> BeginOAuthAsync(MarketingChannel provider,string redirectUri,CancellationToken token=default);
    Task<ProviderOperationResult> CompleteOAuthAsync(MarketingChannel provider,OAuthCallbackRequest request,CancellationToken token=default);
    Task<bool> DisconnectAsync(Guid connectionId,CancellationToken token=default);
    Task<ProviderOperationResult> QueueAsync(Guid postId,bool immediate,CancellationToken token=default);
    Task<int> ProcessDueAsync(CancellationToken token=default);
}
public interface ICampaignEngine { Task<bool> CommandAsync(Guid campaignId,string action,CancellationToken token=default); Task<int> ProcessDueAsync(CancellationToken token=default); }
public interface INewsletterOptInService { Task<string?> SubscribeAsync(NewsletterRequest request,CancellationToken token=default); Task<bool> ConfirmAsync(string token,CancellationToken token2=default); Task<string?> ResendAsync(string email,CancellationToken token=default); }
public interface IAnalyticsService { Task TrackAsync(AnalyticsTrackRequest request,CancellationToken token=default); Task<GrowthReportDto> ReportAsync(DateTimeOffset from,DateTimeOffset to,CancellationToken token=default); }
public interface IAiMarketingService { Task<AiMarketingResult> GenerateAsync(AiMarketingRequest request,CancellationToken token=default); }
