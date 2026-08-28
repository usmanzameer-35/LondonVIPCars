using System.Security.Claims;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Shared.Security;
using LondonVIP.Shared.Tenancy;
using Microsoft.AspNetCore.Http;

namespace LondonVIP.Infrastructure.Security;

public sealed class AuditService(LondonVIPDbContext db, IHttpContextAccessor accessor, ICompanyContext companyContext) : IAuditService
{
    public async Task WriteAsync(string eventType, string category, string outcome, SecurityEventSeverity severity, string description,
        string? resourceType = null, string? resourceIdentifier = null, Guid? companyId = null, CancellationToken cancellationToken = default)
    {
        var context = accessor.HttpContext;
        var userId = context?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        db.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId ?? (context?.User.Identity?.IsAuthenticated == true ? companyContext.CompanyId : null),
            UserId = userId,
            EventType = Safe(eventType, 100) ?? string.Empty, Category = Safe(category, 100) ?? string.Empty, Outcome = Safe(outcome, 50) ?? string.Empty, Severity = severity,
            Timestamp = DateTimeOffset.UtcNow,
            IpAddress = Safe(context?.Connection.RemoteIpAddress?.ToString(), 64),
            UserAgent = Safe(context?.Request.Headers.UserAgent.ToString(), 512),
            CorrelationId = Safe(context?.TraceIdentifier, 100) ?? Guid.NewGuid().ToString("N"),
            ResourceType = Safe(resourceType, 100), ResourceIdentifier = Safe(resourceIdentifier, 200),
            Description = Safe(description, 500) ?? string.Empty
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? Safe(string? value, int length) => string.IsNullOrWhiteSpace(value) ? null : value.Length <= length ? value : value[..length];
}
