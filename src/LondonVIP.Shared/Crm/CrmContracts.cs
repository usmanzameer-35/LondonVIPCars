using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.Crm;

public sealed record CrmLeadRequest(string FirstName, string LastName, string? Email, string? Phone, string Source, int Score, CrmLeadStatus Status, CrmPriority Priority, decimal Probability, decimal ExpectedRevenue, DateTimeOffset? ExpectedCloseAt, DateTimeOffset? FollowUpAt, string? InternalNotes, Guid? OwnerUserId);
public sealed record CrmLeadDto(Guid Id, string Reference, string Name, string? Email, string? Phone, string Source, int Score, CrmLeadStatus Status, CrmPriority Priority, decimal Probability, decimal ExpectedRevenue, DateTimeOffset? FollowUpAt, Guid? CustomerId, DateTimeOffset UpdatedAt);
public sealed record ConvertLeadRequest(bool CreateCustomer = true);
public sealed record PipelineStageRequest(string Name, int SortOrder, decimal DefaultProbability, bool IsWon, bool IsLost, bool IsActive);
public sealed record OpportunityRequest(string Name, Guid PipelineStageId, Guid? LeadId, Guid? CustomerId, Guid? CorporateAccountId, decimal Value, decimal Probability, DateTimeOffset? ExpectedCloseAt, Guid? OwnerUserId, string? WinLossReason);
public sealed record OpportunityDto(Guid Id, string Name, string Stage, decimal Value, decimal Probability, decimal ForecastValue, Guid? LeadId, Guid? CustomerId, Guid? CorporateAccountId, string? WinLossReason, DateTimeOffset UpdatedAt);
public sealed record IncomingMessageRequest(CrmConversationChannel Channel, string ExternalThreadId, string ExternalMessageId, string ParticipantAddress, string Subject, string Body, DateTimeOffset SentAt, string? AttachmentUrl = null, Guid? BookingId = null, Guid? QuotationId = null, Guid? InvoiceId = null);
public sealed record ConversationDto(Guid Id, CrmConversationChannel Channel, string Subject, string ParticipantAddress, string? CustomerName, Guid? LeadId, Guid? BookingId, Guid? QuotationId, Guid? InvoiceId, CrmPriority Priority, bool IsUnread, bool IsPinned, bool IsArchived, DateTimeOffset UpdatedAt, int MessageCount);
public sealed record ConversationUpdateRequest(Guid? AssignedUserId, CrmPriority Priority, string Tags, bool IsUnread, bool IsPinned, bool IsArchived);
public sealed record CrmTaskRequest(CrmTaskType Type, string Title, string? Description, Guid? AssignedUserId, Guid? LeadId, Guid? CustomerId, Guid? CorporateAccountId, DateTimeOffset DueAt, string? Recurrence);
public sealed record CrmReviewRequest(CrmReviewSource Source, string ExternalId, Guid? CustomerId, Guid? BookingId, int Rating, string Body, bool IsComplaint, string? Resolution, DateTimeOffset ReviewedAt);
public sealed record CrmCampaignRequest(string Name, CrmCampaignChannel Channel, string AudienceDefinition, string Status, decimal Cost, DateTimeOffset? StartsAt, DateTimeOffset? EndsAt);
public sealed record TimelineItemDto(Guid Id, CrmActivityType Type, string Subject, string? Details, string? ResourceType, Guid? ResourceId, DateTimeOffset OccurredAt);
public sealed record CrmDashboardDto(int OpenLeads, int Opportunities, decimal ForecastRevenue, int UnreadConversations, int OverdueTasks, int OpenComplaints, decimal CampaignRevenue, decimal ConversionRate);

public interface ICrmService
{
    Task<IReadOnlyList<CrmLeadDto>> SearchLeadsAsync(string? query, CancellationToken token = default);
    Task<CrmLeadDto?> GetLeadAsync(Guid id, CancellationToken token = default);
    Task<CrmLeadDto> CreateLeadAsync(CrmLeadRequest request, CancellationToken token = default);
    Task<CrmLeadDto?> UpdateLeadAsync(Guid id, CrmLeadRequest request, CancellationToken token = default);
    Task<Guid?> ConvertLeadAsync(Guid id, CancellationToken token = default);
    Task<IReadOnlyList<OpportunityDto>> GetPipelineAsync(CancellationToken token = default);
    Task<OpportunityDto?> CreateOpportunityAsync(OpportunityRequest request, CancellationToken token = default);
    Task<OpportunityDto?> MoveOpportunityAsync(Guid id, Guid stageId, string? reason, CancellationToken token = default);
    Task<ConversationDto> IngestAsync(IncomingMessageRequest request, CancellationToken token = default);
    Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(string? query, CrmConversationChannel? channel, CancellationToken token = default);
    Task<IReadOnlyList<TimelineItemDto>> GetTimelineAsync(Guid? customerId, Guid? leadId, CancellationToken token = default);
    Task<CrmDashboardDto> GetDashboardAsync(CancellationToken token = default);
}

public interface ICrmAiService { Task<string?> SuggestReplyAsync(Guid conversationId, CancellationToken token = default); Task<string?> SuggestQuoteAsync(Guid conversationId, CancellationToken token = default); Task<string?> ExtractBookingAsync(Guid conversationId, CancellationToken token = default); Task<string?> DetectSentimentAsync(string text, CancellationToken token = default); Task<string?> DetectLanguageAsync(string text, CancellationToken token = default); Task<string?> TranslateAsync(string text, string language, CancellationToken token = default); Task<string?> SummarizeAsync(Guid conversationId, CancellationToken token = default); Task<bool?> DetectComplaintAsync(string text, CancellationToken token = default); Task<bool?> DetectUrgencyAsync(string text, CancellationToken token = default); }
