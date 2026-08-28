using System.Net;
using System.Net.Http.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Security;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.Bookings;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Security;
using LondonVIP.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace LondonVIP.Tests;

public class SecurityFoundationTests
{
    [Fact]
    public async Task AnonymousOperationalApiAccess_IsBlocked()
    {
        await using var host = await TestApiHost.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/bookings");
        request.Headers.Add("X-Test-Anonymous", "true");
        using var response = await host.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DispatcherCanUseBookings_ButFinanceReceivesForbidden()
    {
        await using var host = await TestApiHost.StartAsync();
        using var dispatcher = new HttpRequestMessage(HttpMethod.Get, "/api/bookings");
        dispatcher.Headers.Add("X-Test-Role", SecurityRoles.Dispatcher);
        using var dispatcherResponse = await host.Client.SendAsync(dispatcher);
        Assert.Equal(HttpStatusCode.OK, dispatcherResponse.StatusCode);

        using var finance = new HttpRequestMessage(HttpMethod.Get, "/api/bookings");
        finance.Headers.Add("X-Test-Role", SecurityRoles.Finance);
        using var financeResponse = await host.Client.SendAsync(finance);
        Assert.Equal(HttpStatusCode.Forbidden, financeResponse.StatusCode);
    }

    [Fact]
    public async Task LoginFailuresLockAccountAndCreateAuditEvents()
    {
        await using var host = await TestApiHost.StartAsync();
        const string email = "lockout@example.test";
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, UserName = email, Email = email, EmailConfirmed = true, CreatedAt = DateTimeOffset.UtcNow, IsActive = true };
            var created = await manager.CreateAsync(user, "Valid-Test-Password-27!");
            Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(error => error.Description)));
        }

        HttpStatusCode last = 0;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new LoginRequest { Email = email, Password = "Wrong-Test-Password!" }) };
            request.Headers.Add("X-Test-Anonymous", "true");
            using var response = await host.Client.SendAsync(request);
            last = response.StatusCode;
        }
        Assert.Equal((HttpStatusCode)423, last);

        await using var auditScope = host.App.Services.CreateAsyncScope();
        var db = auditScope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        Assert.Contains(db.SecurityAuditEvents, item => item.EventType == "AccountLockout" && item.Severity == SecurityEventSeverity.High);
    }

    [Fact]
    public async Task LoginRateLimit_ReturnsTooManyRequests()
    {
        await using var host = await TestApiHost.StartAsync();
        HttpStatusCode last = 0;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new LoginRequest { Email = $"missing{attempt}@example.test", Password = "Wrong-Test-Password!" }) };
            request.Headers.Add("X-Test-Anonymous", "true");
            using var response = await host.Client.SendAsync(request);
            last = response.StatusCode;
        }
        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }

    [Fact]
    public async Task ResponsesIncludeSecurityAndCorrelationHeaders()
    {
        await using var host = await TestApiHost.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/status");
        request.Headers.Add("X-Correlation-ID", "security-test-27");
        using var response = await host.Client.SendAsync(request);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("security-test-27", response.Headers.GetValues("X-Correlation-ID").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.True(response.Headers.Contains("Permissions-Policy"));
    }

    [Fact]
    public async Task ClientSuppliedCompanyId_IsIgnoredAndBookingIsAudited()
    {
        await using var host = await TestApiHost.StartAsync();
        var customer = new Customer { Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, FirstName = "Secure", LastName = "Passenger", Email = $"{Guid.NewGuid():N}@example.test", Phone = "000", CreatedAt = DateTimeOffset.UtcNow, IsActive = true };
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>(); db.Customers.Add(customer); await db.SaveChangesAsync();
        }
        var attackerCompanyId = Guid.NewGuid();
        var payload = new { companyId = attackerCompanyId, customerId = customer.Id, pickupAddress = "Heathrow", destination = "Mayfair", pickupDateTime = DateTimeOffset.UtcNow.AddDays(1), passengerCount = 1, luggageCount = 1, vehicleType = VehicleType.Saloon, baseFare = 50m, extras = 0m, totalFare = 50m, status = BookingStatus.Confirmed, paymentStatus = "Pending" };
        using var response = await host.Client.PostAsJsonAsync("/api/bookings", payload);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<BookingDetailDto>();
        Assert.NotNull(created);

        await using var verifyScope = host.App.Services.CreateAsyncScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        Assert.Equal(LondonVipCompany.Id, verify.Bookings.Single(item => item.Id == created.Id).CompanyId);
        Assert.Contains(verify.SecurityAuditEvents, item => item.EventType == "BookingCreated" && item.ResourceIdentifier == created.Id.ToString());
    }

    [Fact]
    public async Task MalformedProtectedRequest_DoesNotExposeInternalDetails()
    {
        await using var host = await TestApiHost.StartAsync();
        using var response = await host.Client.PostAsync("/api/bookings", new StringContent("{not-json", System.Text.Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("ConnectionStrings", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SqlException", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnonymousErpRoute_DoesNotRenderOperationalDashboard()
    {
        await using var app = LondonVIP.Web.WebProgram.CreateApp(["--environment", "Development"]);
        app.Urls.Add("http://127.0.0.1:0"); await app.StartAsync();
        try
        {
            var server = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
            var address = server.Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false }) { BaseAddress = new Uri(address) };
            using var response = await client.GetAsync("/erp");
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/erp/login", response.Headers.Location?.AbsolutePath);
        }
        finally { await app.StopAsync(); }
    }
}
