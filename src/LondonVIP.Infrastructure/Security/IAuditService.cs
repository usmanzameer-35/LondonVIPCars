using LondonVIP.Shared.Security;

namespace LondonVIP.Infrastructure.Security;

public interface IAuditService
{
    Task WriteAsync(string eventType, string category, string outcome, SecurityEventSeverity severity, string description,
        string? resourceType = null, string? resourceIdentifier = null, Guid? companyId = null, CancellationToken cancellationToken = default);
}
