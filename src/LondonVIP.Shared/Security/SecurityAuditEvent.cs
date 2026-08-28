namespace LondonVIP.Shared.Security;

public sealed class SecurityAuditEvent
{
    public Guid Id { get; set; }
    public Guid? CompanyId { get; set; }
    public string? UserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public SecurityEventSeverity Severity { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string? ResourceType { get; set; }
    public string? ResourceIdentifier { get; set; }
    public string Description { get; set; } = string.Empty;
}
