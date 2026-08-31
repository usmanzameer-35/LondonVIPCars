using System.Security.Claims;
using System.Text.Encodings.Web;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LondonVIP.Tests.Infrastructure;

internal sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.ContainsKey("X-Test-Anonymous")) return Task.FromResult(AuthenticateResult.NoResult());
        var role = Request.Headers["X-Test-Role"].FirstOrDefault() ?? SecurityRoles.Admin;
        var company = Request.Headers["X-Test-Company"].FirstOrDefault() ?? LondonVipCompany.Id.ToString();
        var userId = Request.Headers["X-Test-User"].FirstOrDefault() ?? "test-user";
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Name, "test@example.test"), new Claim(ClaimTypes.Role, role), new Claim("company_id", company) };
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)), SchemeName)));
    }
}
