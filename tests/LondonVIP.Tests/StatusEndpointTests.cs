using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using LondonVIP.Tests.Infrastructure;

namespace LondonVIP.Tests;

public class StatusEndpointTests
{
    [Fact]
    public async Task GetStatus_ReturnsOnlineServiceStatus()
    {
        await using var host = await TestApiHost.StartAsync();
        using var response = await host.Client.GetAsync("/api/status");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
            Assert.Equal("London VIP Cars", body.RootElement.GetProperty("service").GetString());
            Assert.Equal("online", body.RootElement.GetProperty("status").GetString());
    }
}
