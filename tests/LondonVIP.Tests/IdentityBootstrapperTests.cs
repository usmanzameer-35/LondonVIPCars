using LondonVIP.Api.Security;
using LondonVIP.Infrastructure.Security;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.Security;
using LondonVIP.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace LondonVIP.Tests;

public class IdentityBootstrapperTests
{
    private const string Email = "bootstrap.sync@example.test";
    private const string CurrentPassword = "Current-Development-Password-31!";
    private const string OldPassword = "Old-Development-Password-31!";

    [Fact]
    public async Task DevelopmentSynchronizesExistingBootstrapUser_PasswordLockoutActiveStateAndAdminRole()
    {
        await using var host = await TestApiHost.StartAsync();
        var user = await CreateExistingAsync(host, OldPassword, active: false);
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var locked = (await manager.FindByEmailAsync(Email))!;
            await manager.SetLockoutEndDateAsync(locked, DateTimeOffset.UtcNow.AddHours(1));
            locked.AccessFailedCount = 4;
            await manager.UpdateAsync(locked);
            if (await manager.IsInRoleAsync(locked, SecurityRoles.Admin)) await manager.RemoveFromRoleAsync(locked, SecurityRoles.Admin);
        }

        await SynchronizeAsync(host, Environments.Development);

        await using var verifyScope = host.App.Services.CreateAsyncScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var updated = (await verify.FindByEmailAsync(Email))!;
        Assert.True(updated.IsActive);
        Assert.Null(await verify.GetLockoutEndDateAsync(updated));
        Assert.Equal(0, await verify.GetAccessFailedCountAsync(updated));
        Assert.True(await verify.CheckPasswordAsync(updated, CurrentPassword));
        Assert.False(await verify.CheckPasswordAsync(updated, OldPassword));
        Assert.True(await verify.IsInRoleAsync(updated, SecurityRoles.Admin));
    }

    [Fact]
    public async Task ProductionDoesNotResetExistingBootstrapUser()
    {
        await using var host = await TestApiHost.StartAsync();
        var user = await CreateExistingAsync(host, OldPassword, active: false);
        await SynchronizeAsync(host, Environments.Production);

        await using var scope = host.App.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var unchanged = (await manager.FindByEmailAsync(Email))!;
        Assert.False(unchanged.IsActive);
        Assert.True(await manager.CheckPasswordAsync(unchanged, OldPassword));
        Assert.False(await manager.CheckPasswordAsync(unchanged, CurrentPassword));
    }

    [Fact]
    public void BootstrapPasswordIsNotStoredInSourceConfiguration()
    {
        var apiRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "LondonVIP.Api"));
        var configuredFiles = Directory.GetFiles(apiRoot, "appsettings*.json", SearchOption.TopDirectoryOnly);
        Assert.All(configuredFiles, file => Assert.DoesNotContain(CurrentPassword, File.ReadAllText(file), StringComparison.Ordinal));
        var bootstrapper = File.ReadAllText(Path.Combine(apiRoot, "Security", "IdentityBootstrapper.cs"));
        Assert.DoesNotContain("logger.LogInformation(configured.Password", bootstrapper, StringComparison.Ordinal);
        Assert.DoesNotContain("logger.LogError(configured.Password", bootstrapper, StringComparison.Ordinal);
    }

    private static async Task<ApplicationUser> CreateExistingAsync(TestApiHost host, string password, bool active)
    {
        await using var scope = host.App.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, UserName = Email, Email = Email, EmailConfirmed = true, IsActive = active, CreatedAt = DateTimeOffset.UtcNow };
        var result = await manager.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(x => x.Code)));
        return user;
    }

    private static Task SynchronizeAsync(TestApiHost host, string environmentName) => new IdentityBootstrapper(
        host.App.Services,
        Options.Create(new SecurityOptions { BootstrapAdmin = new BootstrapAdminOptions { Email = Email, Password = CurrentPassword } }),
        new FixedEnvironment(environmentName),
        NullLogger<IdentityBootstrapper>.Instance).StartAsync(CancellationToken.None);

    private sealed class FixedEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "LondonVIP.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
