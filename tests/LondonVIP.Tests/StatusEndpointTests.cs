using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace LondonVIP.Tests;

public class StatusEndpointTests
{
    [Fact]
    public async Task GetStatus_ReturnsOnlineServiceStatus()
    {
        await using var app = LondonVIP.Api.Program.CreateApp(["--environment", "Development"]);
        app.Urls.Add("http://127.0.0.1:0");

        await app.StartAsync();

        try
        {
            var server = app.Services.GetRequiredService<IServer>();
            var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();

            using var handler = new HttpClientHandler { UseProxy = false };
            using var client = new HttpClient(handler) { BaseAddress = new Uri(address) };
            using var response = await client.GetAsync("/api/status");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
            Assert.Equal("London VIP Cars", body.RootElement.GetProperty("service").GetString());
            Assert.Equal("online", body.RootElement.GetProperty("status").GetString());
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
