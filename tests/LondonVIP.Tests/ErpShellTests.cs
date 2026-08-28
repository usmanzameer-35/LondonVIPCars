using System.Net;
using LondonVIP.Web.Components.Erp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace LondonVIP.Tests;

public class ErpShellTests
{
    [Fact]
    public void ModuleCatalog_ContainsUniqueRoutesAndCompanySetupLink()
    {
        Assert.Equal(27, ErpModuleCatalog.All.Count);
        Assert.Equal(ErpModuleCatalog.All.Count, ErpModuleCatalog.All.Select(item => item.Route).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(ErpModuleCatalog.All, item => item.Route == "/erp/company-setup" && item.Title == "Company Setup");
        Assert.Contains(ErpModuleCatalog.All, item => item.Title == "Leads & CRM");
        Assert.Contains(ErpModuleCatalog.All, item => item.Title == "Live Journey Intelligence");
    }

    [Fact]
    public async Task Dashboard_RendersSummarySectionsAndModuleNavigation()
    {
        await WithWebAppAsync(async client =>
        {
            var html = await client.GetStringAsync("/erp");

            Assert.Contains("Operations dashboard", html);
            Assert.Contains("Bookings Today", html);
            Assert.Contains("Next 2 Hours", html);
            Assert.Contains("Flight Delays", html);
            Assert.Contains("Expiring Documents", html);
            Assert.Contains("Demonstration data", html);
            Assert.Contains("href=\"/erp/company-setup\"", html);
            Assert.Contains("Website / CMS", html);
        });
    }

    [Theory]
    [InlineData("/erp/quotes", "Quotes", "Development preview")]
    [InlineData("/erp/leads", "Leads &amp; CRM", "Conversion reporting")]
    [InlineData("/erp/website-cms", "Website / CMS", "Homepage management")]
    [InlineData("/erp/insights", "Insights / Travel Hub", "TfL / TPH updates")]
    [InlineData("/erp/journey-intelligence", "Live Journey Intelligence", "Journey-risk alerts")]
    [InlineData("/erp/company-setup", "Company setup", "Save changes")]
    public async Task ErpRoute_RendersExpectedModuleShell(string route, string title, string expectedContent)
    {
        await WithWebAppAsync(async client =>
        {
            using var response = await client.GetAsync(route);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(title, html);
            Assert.Contains(expectedContent, html);
            Assert.Contains("London VIP Cars", html);
        });
    }

    private static async Task WithWebAppAsync(Func<HttpClient, Task> test)
    {
        await using var app = LondonVIP.Web.WebProgram.CreateApp(["--environment", "Development"]);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        try
        {
            var server = app.Services.GetRequiredService<IServer>();
            var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var handler = new HttpClientHandler { UseProxy = false, AllowAutoRedirect = true };
            using var client = new HttpClient(handler) { BaseAddress = new Uri(address) };
            await test(client);
        }
        finally { await app.StopAsync(); }
    }
}
