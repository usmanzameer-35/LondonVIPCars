using LondonVIP.Infrastructure.Security;
using LondonVIP.Shared.Security;
using Microsoft.AspNetCore.Identity;

namespace LondonVIP.Api.Security;

public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/auth/login", LoginAsync).AllowAnonymous().RequireRateLimiting("login");
        endpoints.MapPost("/api/auth/logout", LogoutAsync).RequireAuthorization();
        endpoints.MapGet("/api/auth/session", SessionAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> LoginAsync(LoginRequest request, UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn, IAuditService audit, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 254 || string.IsNullOrEmpty(request.Password) || request.Password.Length > 256)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["credentials"] = ["Valid email and password are required."] });

        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive)
        {
            await audit.WriteAsync("LoginFailure", "Authentication", "Denied", SecurityEventSeverity.Warning, "Login failed for an unknown or inactive account.", cancellationToken: cancellationToken);
            return Results.Unauthorized();
        }

        var result = await signIn.PasswordSignInAsync(user, request.Password, request.RememberMe, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            await audit.WriteAsync("AccountLockout", "Authentication", "Denied", SecurityEventSeverity.High, "Account locked after repeated login failures.", "User", user.Id.ToString(), user.CompanyId, cancellationToken);
            return Results.Problem(statusCode: StatusCodes.Status423Locked, title: "Account temporarily locked");
        }
        if (!result.Succeeded)
        {
            await audit.WriteAsync("LoginFailure", "Authentication", "Denied", SecurityEventSeverity.Warning, "Login failed due to invalid credentials.", "User", user.Id.ToString(), user.CompanyId, cancellationToken);
            return Results.Unauthorized();
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await users.UpdateAsync(user);
        await audit.WriteAsync("LoginSuccess", "Authentication", "Succeeded", SecurityEventSeverity.Information, "ERP account signed in.", "User", user.Id.ToString(), user.CompanyId, cancellationToken);
        return Results.Ok(await ToSessionAsync(user, users));
    }

    private static async Task<IResult> LogoutAsync(SignInManager<ApplicationUser> signIn, IAuditService audit, CancellationToken cancellationToken)
    {
        await audit.WriteAsync("Logout", "Authentication", "Succeeded", SecurityEventSeverity.Information, "ERP account signed out.", cancellationToken: cancellationToken);
        await signIn.SignOutAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> SessionAsync(System.Security.Claims.ClaimsPrincipal principal, UserManager<ApplicationUser> users)
    {
        var user = await users.GetUserAsync(principal);
        return user is null ? Results.Unauthorized() : Results.Ok(await ToSessionAsync(user, users));
    }

    private static async Task<AuthSessionDto> ToSessionAsync(ApplicationUser user, UserManager<ApplicationUser> users) => new()
    {
        UserId = user.Id.ToString(), CompanyId = user.CompanyId, Email = user.Email ?? string.Empty,
        Roles = (await users.GetRolesAsync(user)).ToArray()
    };
}
