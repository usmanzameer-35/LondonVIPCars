namespace LondonVIP.Api.Security;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";
    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].FirstOrDefault();
        context.TraceIdentifier = IsSafe(supplied) ? supplied! : Guid.NewGuid().ToString("N");
        context.Response.Headers[HeaderName] = context.TraceIdentifier;
        await next(context);
    }

    private static bool IsSafe(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 100 && value.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_');
}
