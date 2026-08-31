namespace LondonVIP.Shared.Models;

public enum WorkflowJobStatus { Waiting, Scheduled, Running, Completed, Failed, Cancelled, Retrying, Escalated }
public enum WorkflowJobKind { OneTime, Delayed, Recurring, Retry, Escalation, Future }

public sealed class BusinessEventRecord
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public Company Company { get; set; }=null!;
    public string EventType { get; set; }=""; public string ResourceType { get; set; }=""; public Guid? ResourceId { get; set; }
    public string PayloadJson { get; set; }="{}"; public string CorrelationId { get; set; }=""; public DateTimeOffset OccurredAt { get; set; }
}
public sealed class WorkflowJob
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public Company Company { get; set; }=null!;
    public Guid? BusinessEventId { get; set; } public BusinessEventRecord? BusinessEvent { get; set; }
    public string WorkflowType { get; set; }=""; public WorkflowJobKind Kind { get; set; } public WorkflowJobStatus Status { get; set; }
    public string PayloadJson { get; set; }="{}"; public string CorrelationId { get; set; }=""; public DateTimeOffset ScheduledAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; } public DateTimeOffset? CompletedAt { get; set; } public DateTimeOffset? LastAttemptAt { get; set; }
    public int AttemptCount { get; set; } public int MaxAttempts { get; set; }=3; public int EscalationLevel { get; set; }
    public string? LastError { get; set; } public string? Recurrence { get; set; } public DateTimeOffset CreatedAt { get; set; } public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class WorkflowRule
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public Company Company { get; set; }=null!;
    public string Name { get; set; }=""; public string EventType { get; set; }=""; public string ConditionField { get; set; }="";
    public string Operator { get; set; }=""; public string ComparisonValue { get; set; }=""; public string Action { get; set; }="";
    public int Priority { get; set; } public bool IsActive { get; set; }=true; public DateTimeOffset CreatedAt { get; set; } public DateTimeOffset UpdatedAt { get; set; }
}
