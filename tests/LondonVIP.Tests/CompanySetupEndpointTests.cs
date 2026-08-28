using System.Net;
using System.Net.Http.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.CompanySetup;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LondonVIP.Tests;

public class CompanySetupEndpointTests
{
    [Fact]
    public async Task GetSetup_LoadsLondonVipCompanySetup()
    {
        await WithAppAsync(null, async (_, client) =>
        {
            var setup = await client.GetFromJsonAsync<CompanySetupDto>("/api/company/setup");

            Assert.NotNull(setup);
            Assert.Equal("London VIP Cars", setup.Profile.TradingName);
            Assert.Equal("Europe/London", setup.Profile.TimeZone);
            Assert.Equal("GBP", setup.Profile.Currency);
            Assert.Equal("#153F37", setup.Branding.PrimaryColour);
            Assert.Equal("LVC", setup.Invoice.InvoicePrefix);
            Assert.Equal("London VIP Cars", setup.Website.WebsiteTitle);
            Assert.Null(typeof(CompanySetupDto).GetProperty("CompanyId"));
        });
    }

    [Fact]
    public async Task PutSetup_UpdatesOnlyCurrentCompany()
    {
        await WithAppAsync(null, async (app, client) =>
        {
            Company original;
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
                original = await db.Companies.AsNoTracking().Include(item => item.Settings).Include(item => item.Branding)
                    .SingleAsync(item => item.Id == LondonVipCompany.Id);
            }

            try
            {
                var setup = await client.GetFromJsonAsync<CompanySetupDto>("/api/company/setup");
                Assert.NotNull(setup);
                MakeValid(setup);
                setup.Website.WebsiteTagline = "Tenant-safe setup test";

                using var response = await client.PutAsJsonAsync("/api/company/setup", setup);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var updated = await response.Content.ReadFromJsonAsync<CompanySetupDto>();
                Assert.Equal("Tenant-safe setup test", updated?.Website.WebsiteTagline);

                await using var verifyScope = app.Services.CreateAsyncScope();
                var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
                Assert.Equal("Tenant-safe setup test", await verifyDb.CompanyBranding
                    .Where(item => item.CompanyId == LondonVipCompany.Id)
                    .Select(item => item.CustomerWebsiteTagline).SingleAsync());
            }
            finally
            {
                await using var restoreScope = app.Services.CreateAsyncScope();
                var restoreDb = restoreScope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
                var company = await restoreDb.Companies.Include(item => item.Settings).Include(item => item.Branding)
                    .SingleAsync(item => item.Id == LondonVipCompany.Id);
                restoreDb.Entry(company).CurrentValues.SetValues(original);
                restoreDb.Entry(company.Settings!).CurrentValues.SetValues(original.Settings!);
                restoreDb.Entry(company.Branding!).CurrentValues.SetValues(original.Branding!);
                await restoreDb.SaveChangesAsync();
            }
        });
    }

    [Fact]
    public async Task GetSetup_DoesNotFallBackToLondonVip_WhenTenantIsDifferent()
    {
        var otherCompanyId = Guid.NewGuid();
        await WithAppAsync(
            services =>
            {
                services.RemoveAll<ICompanyContext>();
                services.AddSingleton<ICompanyContext>(new FixedCompanyContext(otherCompanyId));
            },
            async (_, client) =>
            {
                using var response = await client.GetAsync("/api/company/setup");
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            });
    }

    [Fact]
    public async Task PutSetup_ReturnsValidationProblem_AndDoesNotUpdateCompany()
    {
        await WithAppAsync(null, async (app, client) =>
        {
            DateTimeOffset updatedAt;
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
                updatedAt = await db.Companies.Where(item => item.Id == LondonVipCompany.Id).Select(item => item.UpdatedAt).SingleAsync();
            }

            var invalid = new CompanySetupDto
            {
                Operations = { MinimumBookingNoticeMinutes = -1, DriverCommissionPercentage = 101 },
                Invoice = { VatEnabled = false, VatRate = 20 }
            };
            using var response = await client.PutAsJsonAsync("/api/company/setup", invalid);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<ValidationResponse>();
            Assert.NotNull(problem);
            Assert.Contains("profile.tradingName", problem.Errors.Keys, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("operations.minimumBookingNoticeMinutes", problem.Errors.Keys, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("invoice.vatRate", problem.Errors.Keys, StringComparer.OrdinalIgnoreCase);

            await using var verifyScope = app.Services.CreateAsyncScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
            Assert.Equal(updatedAt, await verifyDb.Companies.Where(item => item.Id == LondonVipCompany.Id).Select(item => item.UpdatedAt).SingleAsync());
        });
    }

    private static void MakeValid(CompanySetupDto setup)
    {
        setup.Profile.LegalName = "London VIP Cars Test Legal Name";
        setup.Profile.Email = "operations@example.test";
        setup.Profile.Phone = "020 0000 0000";
        setup.Profile.Address = "Test address";
        setup.Profile.Postcode = "SW1A 1AA";
        setup.Profile.Country = "United Kingdom";
        setup.Operations.DefaultLanguage = "en-GB";
        setup.Invoice.InvoicePrefix = "LVC";
    }

    private static async Task WithAppAsync(
        Action<IServiceCollection>? configureServices,
        Func<WebApplication, HttpClient, Task> test)
    {
        await using var app = LondonVIP.Api.Program.CreateApp(["--environment", "Development"], configureServices);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        try
        {
            var server = app.Services.GetRequiredService<IServer>();
            var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var handler = new HttpClientHandler { UseProxy = false };
            using var client = new HttpClient(handler) { BaseAddress = new Uri(address) };
            await test(app, client);
        }
        finally { await app.StopAsync(); }
    }

    private sealed class FixedCompanyContext(Guid companyId) : ICompanyContext
    {
        public Guid CompanyId { get; } = companyId;
    }

    private sealed class ValidationResponse
    {
        public Dictionary<string, string[]> Errors { get; set; } = [];
    }
}
