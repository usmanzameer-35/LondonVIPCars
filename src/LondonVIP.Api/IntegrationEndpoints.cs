using LondonVIP.Infrastructure.Integrations;
using LondonVIP.Infrastructure.Security;
using LondonVIP.Shared.Integrations;
using LondonVIP.Shared.Security;
using LondonVIP.Shared.Tenancy;

namespace LondonVIP.Api;

public static class IntegrationEndpoints
{
    public static IEndpointRouteBuilder MapIntegrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/integrations").RequireAuthorization(SecurityPolicies.PlatformAdministration).RequireRateLimiting("operations");
        group.MapGet("", DashboardAsync);
        group.MapGet("/health", HealthAsync);
        group.MapGet("/providers", Providers);
        group.MapGet("/configuration", Configuration);
        group.MapGet("/diagnostics", DashboardAsync);
        group.MapPost("/connections/test", TestConnectionAsync);
        group.MapPost("/webhooks/test", TestWebhookAsync);
        group.MapPost("/webhooks/{providerKey}/{eventType}", ReceiveWebhookAsync);
        group.MapGet("/webhooks", ListWebhooksAsync);
        group.MapPost("/webhooks/{id:guid}/retry", RetryWebhookAsync);
        return endpoints;
    }
    private static async Task<IResult> ListWebhooksAsync(WebhookDeliveryState? status, IWebhookAdministrationService service, CancellationToken token) => Results.Ok(await service.ListAsync(status, token));
    private static async Task<IResult> RetryWebhookAsync(Guid id, IWebhookAdministrationService service, IAuditService audit, ICompanyContext company, CancellationToken token)
    {
        var updated = await service.RetryAsync(id, token); if (!updated) return Results.NotFound();
        await audit.WriteAsync("IntegrationWebhookRetried", "Integrations", "Succeeded", SecurityEventSeverity.Warning, "A failed webhook was queued for retry.", "Webhook", id.ToString(), company.CompanyId, token);
        return Results.Accepted();
    }

    private static async Task<IResult> DashboardAsync(IntegrationDiagnosticsService diagnostics, IAuditService audit, ICompanyContext company, CancellationToken token)
    {
        var result = await diagnostics.GetDashboardAsync(token);
        await audit.WriteAsync("IntegrationDashboardViewed", "Integrations", "Succeeded", SecurityEventSeverity.Information, "Integration diagnostics viewed.", companyId: company.CompanyId, cancellationToken: token);
        return Results.Ok(result);
    }

    private static async Task<IResult> HealthAsync(IntegrationDiagnosticsService diagnostics, CancellationToken token) => Results.Ok((await diagnostics.GetDashboardAsync(token)).Health);
    private static IResult Providers(IProviderRegistry registry) => Results.Ok(registry.Describe());
    private static IResult Configuration(IProviderRegistry registry) => Results.Ok(registry.Describe().Select(x => new IntegrationConfigurationDto(x.Key, x.Category, x.IsEnabled, new Dictionary<string, string>(), x.IsConfigured)));

    private static async Task<IResult> TestConnectionAsync(ConnectionTestRequest request, IntegrationDiagnosticsService diagnostics, IAuditService audit, ICompanyContext company, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderKey) || request.ProviderKey.Length > 100) return Results.ValidationProblem(new Dictionary<string, string[]> { ["providerKey"] = ["A valid provider key is required."] });
        var result = await diagnostics.TestAsync(request.ProviderKey, token);
        await audit.WriteAsync("IntegrationConnectionTested", "Integrations", result.Success ? "Succeeded" : "Failed", SecurityEventSeverity.Information, $"Integration connection test completed for {request.ProviderKey}.", companyId: company.CompanyId, cancellationToken: token);
        return Results.Ok(result);
    }

    private static Task<IResult> TestWebhookAsync(WebhookTestRequest request, IWebhookEngine engine, ICompanyContext company, CancellationToken token) => ProcessWebhook(request.ProviderKey, request.EventType, request.Payload, request.Signature, request.DeliveryId, engine, company.CompanyId, token);
    private static Task<IResult> ReceiveWebhookAsync(string providerKey, string eventType, WebhookTestRequest request, IWebhookEngine engine, ICompanyContext company, CancellationToken token) => ProcessWebhook(providerKey, eventType, request.Payload, request.Signature, request.DeliveryId, engine, company.CompanyId, token);
    private static async Task<IResult> ProcessWebhook(string providerKey, string eventType, string payload, string? signature, string? deliveryId, IWebhookEngine engine, Guid companyId, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(providerKey) || string.IsNullOrWhiteSpace(eventType) || payload.Length > 1_000_000) return Results.BadRequest();
        var scopedDeliveryId = $"{companyId:N}:{deliveryId ?? Guid.NewGuid().ToString("N")}";
        var result = await engine.ReceiveAsync(providerKey, eventType, payload, signature, scopedDeliveryId, token);
        return result.Accepted ? Results.Accepted(value: result) : result.Duplicate ? Results.Conflict(result) : Results.BadRequest(result);
    }
}
