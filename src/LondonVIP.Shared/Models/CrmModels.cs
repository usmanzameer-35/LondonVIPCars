namespace LondonVIP.Shared.Models;

public enum CrmLeadStatus { New, Contacted, Qualified, Unqualified, Converted, Lost }
public enum CrmPriority { Low, Normal, High, Urgent }
public enum CrmActivityType { Call, Email, Sms, WhatsApp, Messenger, Instagram, LinkedIn, GoogleBusiness, Quote, Booking, Invoice, Payment, Review, Note, Task, Document, StatusChange, Workflow, Audit }
public enum CrmConversationChannel { WhatsApp, FacebookMessenger, InstagramDirect, GoogleBusiness, Email, Sms, Voice, WebsiteContactForm, LinkedIn, LiveChat }
public enum CrmTaskType { Task, Meeting, PhoneCall, Callback, Reminder }
public enum CrmTaskStatus { Open, InProgress, Completed, Cancelled }
public enum CrmReviewSource { Google, Facebook, Manual }
public enum CrmCampaignChannel { Email, Sms, WhatsApp, Mixed }

public sealed class CrmLead
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public Company Company { get; set; } = null!;
    public string Reference { get; set; } = string.Empty; public string FirstName { get; set; } = string.Empty; public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; } public string? Phone { get; set; } public string Source { get; set; } = string.Empty; public int Score { get; set; }
    public Guid? OwnerUserId { get; set; } public CrmLeadStatus Status { get; set; } public CrmPriority Priority { get; set; } public decimal Probability { get; set; }
    public decimal ExpectedRevenue { get; set; } public DateTimeOffset? ExpectedCloseAt { get; set; } public DateTimeOffset? FollowUpAt { get; set; }
    public string? InternalNotes { get; set; } public Guid? CustomerId { get; set; } public Customer? Customer { get; set; }
    public DateTimeOffset CreatedAt { get; set; } public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class CrmPipelineStage
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public Company Company { get; set; } = null!; public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; } public decimal DefaultProbability { get; set; } public bool IsWon { get; set; } public bool IsLost { get; set; } public bool IsActive { get; set; } = true;
}
public sealed class CrmOpportunity
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public Company Company { get; set; } = null!; public Guid? LeadId { get; set; } public CrmLead? Lead { get; set; }
    public Guid? CustomerId { get; set; } public Customer? Customer { get; set; } public Guid? CorporateAccountId { get; set; } public CorporateAccount? CorporateAccount { get; set; }
    public Guid PipelineStageId { get; set; } public CrmPipelineStage PipelineStage { get; set; } = null!; public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; } public decimal Probability { get; set; } public DateTimeOffset? ExpectedCloseAt { get; set; } public Guid? OwnerUserId { get; set; }
    public string? WinLossReason { get; set; } public DateTimeOffset CreatedAt { get; set; } public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class CrmConversation
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public Company Company { get; set; } = null!; public CrmConversationChannel Channel { get; set; }
    public string ExternalThreadId { get; set; } = string.Empty; public string Subject { get; set; } = string.Empty; public string ParticipantAddress { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; } public Customer? Customer { get; set; } public Guid? LeadId { get; set; } public CrmLead? Lead { get; set; }
    public Guid? BookingId { get; set; } public Booking? Booking { get; set; } public Guid? QuotationId { get; set; } public Quotation? Quotation { get; set; }
    public Guid? InvoiceId { get; set; } public Invoice? Invoice { get; set; } public Guid? AssignedUserId { get; set; } public CrmPriority Priority { get; set; }
    public string Tags { get; set; } = string.Empty; public bool IsUnread { get; set; } public bool IsPinned { get; set; } public bool IsArchived { get; set; }
    public DateTimeOffset CreatedAt { get; set; } public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<CrmMessage> Messages { get; set; } = [];
}
public sealed class CrmMessage
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public CrmConversation Conversation { get; set; } = null!; public Guid ConversationId { get; set; }
    public string ExternalMessageId { get; set; } = string.Empty; public bool IsInbound { get; set; } public string Sender { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty; public string? AttachmentUrl { get; set; } public string Status { get; set; } = string.Empty;
    public DateTimeOffset SentAt { get; set; } public DateTimeOffset CreatedAt { get; set; }
}
public sealed class CrmActivity
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public CrmActivityType Type { get; set; } public Guid? CustomerId { get; set; }
    public Guid? LeadId { get; set; } public Guid? CorporateAccountId { get; set; } public Guid? ConversationId { get; set; } public string Subject { get; set; } = string.Empty;
    public string? Details { get; set; } public string? ResourceType { get; set; } public Guid? ResourceId { get; set; } public Guid? UserId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
public sealed class CrmTask
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public CrmTaskType Type { get; set; } public CrmTaskStatus Status { get; set; }
    public string Title { get; set; } = string.Empty; public string? Description { get; set; } public Guid? AssignedUserId { get; set; } public Guid? LeadId { get; set; }
    public Guid? CustomerId { get; set; } public Guid? CorporateAccountId { get; set; } public DateTimeOffset DueAt { get; set; } public string? Recurrence { get; set; }
    public DateTimeOffset CreatedAt { get; set; } public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class CrmDocument
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public Guid? CustomerId { get; set; } public Guid? LeadId { get; set; } public Guid? CorporateAccountId { get; set; }
    public string Category { get; set; } = string.Empty; public string FileName { get; set; } = string.Empty; public string StoragePath { get; set; } = string.Empty;
    public int Version { get; set; } = 1; public Guid? PreviousVersionId { get; set; } public DateTimeOffset CreatedAt { get; set; }
}
public sealed class CrmReview
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public CrmReviewSource Source { get; set; } public string ExternalId { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; } public Guid? BookingId { get; set; } public int Rating { get; set; } public string Body { get; set; } = string.Empty;
    public bool IsComplaint { get; set; } public string? Resolution { get; set; } public DateTimeOffset ReviewedAt { get; set; } public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class CrmCampaign
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public string Name { get; set; } = string.Empty; public CrmCampaignChannel Channel { get; set; }
    public string AudienceDefinition { get; set; } = string.Empty; public string Status { get; set; } = string.Empty; public decimal Cost { get; set; }
    public int SentCount { get; set; } public int OpenCount { get; set; } public int ClickCount { get; set; } public int BookingCount { get; set; }
    public decimal Revenue { get; set; } public DateTimeOffset? StartsAt { get; set; } public DateTimeOffset? EndsAt { get; set; } public DateTimeOffset CreatedAt { get; set; }
}
