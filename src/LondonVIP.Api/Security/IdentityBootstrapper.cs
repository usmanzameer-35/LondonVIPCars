using LondonVIP.Infrastructure.Security;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace LondonVIP.Api.Security;

public sealed class IdentityBootstrapper(IServiceProvider services, IOptions<SecurityOptions> options, ILogger<IdentityBootstrapper> logger) : IHostedService
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
        if (await users.FindByEmailAsync(configured.Email) is not null) return;
        var user = new ApplicationUser { Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, UserName = configured.Email.Trim(), Email = configured.Email.Trim(), EmailConfirmed = true, IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        var result = await users.CreateAsync(user, configured.Password);
        if (result.Succeeded) await users.AddToRoleAsync(user, SecurityRoles.Admin);
        else logger.LogError("Configured bootstrap administrator could not be created: {Errors}", string.Join("; ", result.Errors.Select(error => error.Code)));
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
