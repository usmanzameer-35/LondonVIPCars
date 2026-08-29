using LondonVIP.Infrastructure.Security;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace LondonVIP.Api.Security;

public sealed class IdentityBootstrapper(IServiceProvider services, IOptions<SecurityOptions> options, IHostEnvironment environment, ILogger<IdentityBootstrapper> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in SecurityRoles.All)
            if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole<Guid>(role));

        var configured = options.Value.BootstrapAdmin;
        if (string.IsNullOrWhiteSpace(configured.Email) || string.IsNullOrWhiteSpace(configured.Password)) return;
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var existing = await users.FindByEmailAsync(configured.Email);
        if (existing is not null)
        {
            // Never modify established users outside Development. Development may intentionally synchronize
            // a local bootstrap secret so a stale password/lockout does not prevent local ERP access.
            if (!environment.IsDevelopment()) return;

            existing.IsActive = true;
            await users.SetLockoutEndDateAsync(existing, null);
            await users.ResetAccessFailedCountAsync(existing);
            var resetToken = await users.GeneratePasswordResetTokenAsync(existing);
            var reset = await users.ResetPasswordAsync(existing, resetToken, configured.Password);
            if (!reset.Succeeded)
            {
                logger.LogError("Development bootstrap administrator password synchronization failed: {Errors}", string.Join("; ", reset.Errors.Select(error => error.Code)));
                return;
            }
            if (!await users.IsInRoleAsync(existing, SecurityRoles.Admin))
                await users.AddToRoleAsync(existing, SecurityRoles.Admin);
            logger.LogInformation("Development bootstrap administrator account was synchronized.");
            return;
        }
        var user = new ApplicationUser { Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, UserName = configured.Email.Trim(), Email = configured.Email.Trim(), EmailConfirmed = true, IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        var result = await users.CreateAsync(user, configured.Password);
        if (result.Succeeded) await users.AddToRoleAsync(user, SecurityRoles.Admin);
        else logger.LogError("Configured bootstrap administrator could not be created: {Errors}", string.Join("; ", result.Errors.Select(error => error.Code)));
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
