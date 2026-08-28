using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LondonVIP.Tests.Infrastructure;

namespace LondonVIP.Tests;

public class TenantFoundationTests
{
    [Fact]
    public void TenantModels_CanBeInstantiated()
    {
        var company = new Company
        {
            Id = LondonVipCompany.Id,
            TradingName = "London VIP Cars",
            Slug = LondonVipCompany.Slug,
            CurrencyCode = "GBP",
            TimeZone = "Europe/London",
            IsActive = true
        };
        var settings = new CompanySettings { CompanyId = company.Id, DefaultLanguage = "en-GB" };
        var branding = new CompanyBranding { CompanyId = company.Id, CustomerWebsiteTitle = company.TradingName };

        Assert.Equal("london-vip-cars", company.Slug);
        Assert.Equal(company.Id, settings.CompanyId);
        Assert.Equal(company.Id, branding.CompanyId);
    }

    [Fact]
    public void EfModel_DefinesTenantOwnership_WhileAirportRemainsGlobal()
    {
        var options = new DbContextOptionsBuilder<LondonVIPDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var context = new LondonVIPDbContext(options);

        foreach (var entityType in new[] { typeof(Customer), typeof(Driver), typeof(Vehicle), typeof(Booking), typeof(PricingRule) })
        {
            var entity = context.Model.FindEntityType(entityType);
            Assert.NotNull(entity);
            Assert.NotNull(entity.FindProperty("CompanyId"));
            Assert.Contains(entity.GetForeignKeys(), foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Company));
        }

        var airport = context.Model.FindEntityType(typeof(Airport));
        Assert.NotNull(airport);
        Assert.Null(airport.FindProperty("CompanyId"));
        Assert.DoesNotContain(airport.GetForeignKeys(), foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Company));
    }

    [Fact]
    public async Task DevelopmentDatabase_ContainsDefaultCompanySettingsAndBranding()
    {
        await using var host = await TestApiHost.StartAsync();
        await using var scope = host.App.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();

        var company = await context.Companies.AsNoTracking()
            .SingleAsync(item => item.Id == LondonVipCompany.Id);
        var settings = await context.CompanySettings.AsNoTracking()
            .SingleAsync(item => item.CompanyId == LondonVipCompany.Id);
        var branding = await context.CompanyBranding.AsNoTracking()
            .SingleAsync(item => item.CompanyId == LondonVipCompany.Id);

        Assert.Equal("London VIP Cars", company.TradingName);
        Assert.Equal("london-vip-cars", company.Slug);
        Assert.Equal("GBP", company.CurrencyCode);
        Assert.Equal("Europe/London", company.TimeZone);
        Assert.True(company.IsActive);
        Assert.Equal(0m, settings.WaitingChargePerHour);
        Assert.Equal("London VIP Cars", branding.CustomerWebsiteTitle);
        Assert.Equal(4, await context.Airports.CountAsync());
    }
}
