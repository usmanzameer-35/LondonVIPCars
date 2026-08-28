using LondonVIP.Infrastructure.Security;
using LondonVIP.Shared.Security;

namespace LondonVIP.Api.Security;

public sealed class AuthorizationAuditMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAuditService audit)
    {
        await next(context);
        if (context.Response.StatusCode is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden &&
            !context.Request.Path.StartsWithSegments("/api/auth/login"))
        {
            await audit.WriteAsync("AuthorizationFailure", "Authorization", "Denied", SecurityEventSeverity.Warning,
                "A protected resource request was denied.", "HttpEndpoint", context.Request.Path, cancellationToken: context.RequestAborted);
        }
    }
}
