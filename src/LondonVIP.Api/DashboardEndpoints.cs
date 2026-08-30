using LondonVIP.Infrastructure.Security;
using LondonVIP.Shared.Dashboard;
using LondonVIP.Shared.Security;
using LondonVIP.Shared.Tenancy;

namespace LondonVIP.Api;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/dashboard").RequireAuthorization(SecurityPolicies.ErpAccess).RequireRateLimiting("operations");
        group.MapGet("", async (IDashboardService service, IAuditService audit, ICompanyContext company, CancellationToken token) =>
        {
            await AuditAsync(audit, company, "Dashboard", token);
            return Results.Ok(await service.GetDashboardAsync(token));
        });
        group.MapGet("/revenue", async (IDashboardService service, IAuditService audit, ICompanyContext company, CancellationToken token) =>
        {
            await AuditAsync(audit, company, "Revenue", token);
            return Results.Ok(await service.GetRevenueAsync(token));
        });
        group.MapGet("/bookings", async (IDashboardService service, IAuditService audit, ICompanyContext company, CancellationToken token) =>
        {
            await AuditAsync(audit, company, "Bookings", token);
            return Results.Ok(await service.GetBookingsAsync(token));
        });
        group.MapGet("/operations", async (IDashboardService service, IAuditService audit, ICompanyContext company, CancellationToken token) =>
        {
            await AuditAsync(audit, company, "Operations", token);
            return Results.Ok(await service.GetOperationsAsync(token));
        });
        group.MapGet("/drivers", async (IDashboardService service, IAuditService audit, ICompanyContext company, CancellationToken token) =>
        {
            await AuditAsync(audit, company, "Drivers", token);
            return Results.Ok(await service.GetDriversAsync(token));
        });
        return endpoints;
    }

    private static Task AuditAsync(IAuditService audit, ICompanyContext company, string section, CancellationToken token) =>
        audit.WriteAsync("DashboardViewed", "Reporting", "Succeeded", SecurityEventSeverity.Information, $"{section} dashboard data viewed.", "Dashboard", section, company.CompanyId, token);
}
