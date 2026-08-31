using System.Net;
using System.Net.Http.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Security;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.Drivers;
using LondonVIP.Shared.Maps;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Security;
using LondonVIP.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LondonVIP.Tests;

public sealed class DriverPortalEndpointTests
{
    [Fact]
    public async Task DriverCanOperateOwnDayAndCannotImpersonateAnotherDriver()
    {
        await using var host = await TestApiHost.StartAsync(); var data = await SeedAsync(host); Authorize(host, data.UserId);
        using var dashboardResponse = await host.Client.GetAsync("/api/driver/dashboard"); var dashboardBody = await dashboardResponse.Content.ReadAsStringAsync(); Assert.True(dashboardResponse.IsSuccessStatusCode, dashboardBody); var dashboard = await dashboardResponse.Content.ReadFromJsonAsync<DriverPortalDashboardDto>(); Assert.NotNull(dashboard); Assert.Equal(data.DriverId, dashboard.Profile.DriverId);
        var jobs = await host.Client.GetFromJsonAsync<List<DriverPortalJobDto>>("/api/driver/jobs"); var assignedJob = Assert.Single(jobs!); Assert.Equal(data.BookingId, assignedJob.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/driver/jobs/{data.OtherBookingId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsync("/api/driver/shift/start", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsync("/api/driver/status/online", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsync($"/api/driver/jobs/{data.BookingId}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsync($"/api/driver/jobs/{data.BookingId}/enroute", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsync($"/api/driver/jobs/{data.BookingId}/arrived", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsync($"/api/driver/jobs/{data.BookingId}/onboard", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsync($"/api/driver/jobs/{data.BookingId}/complete", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsync("/api/driver/shift/end", null)).StatusCode);
        await using var scope = host.App.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>(); Assert.Equal(BookingStatus.Completed, (await db.Bookings.FindAsync(data.BookingId))!.Status); Assert.Contains(db.SecurityAuditEvents, x => x.EventType == "DriverOnline"); Assert.Contains(db.BusinessEvents, x => x.EventType == "ShiftStarted");
    }

    [Fact]
    public async Task ComplianceLocationDocumentsEarningsAndVehicleIssuesAreTenantSafe()
    {
        await using var host = await TestApiHost.StartAsync(); var data = await SeedAsync(host); Authorize(host, data.UserId);
        var location = new DriverLocationUpdateDto(data.OtherDriverId, data.BookingId, data.VehicleId, 51.5, -0.1, 20, 10, 4, DateTimeOffset.UtcNow);
        Assert.Equal(HttpStatusCode.Accepted, (await host.Client.PostAsJsonAsync("/api/driver/location", location)).StatusCode);
        await using (var scope = host.App.Services.CreateAsyncScope()) { var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>(); Assert.Contains(db.DriverLocations, x => x.DriverId == data.DriverId); Assert.DoesNotContain(db.DriverLocations, x => x.DriverId == data.OtherDriverId); }
        Assert.NotNull(await host.Client.GetFromJsonAsync<List<DriverDocumentDto>>("/api/driver/documents")); Assert.NotNull(await host.Client.GetFromJsonAsync<DriverEarningsDto>("/api/driver/earnings")); Assert.NotNull(await host.Client.GetFromJsonAsync<DriverPortalVehicleDto>("/api/driver/vehicle"));
        var issue = new VehicleIssueRequest("Breakdown", "High", "Engine warning and loss of power.", data.BookingId); Assert.Equal(HttpStatusCode.Created, (await host.Client.PostAsJsonAsync("/api/driver/vehicle/issues", issue)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/api/driver/location", location with { Latitude = 100 })).StatusCode);
        using var anonymous = new HttpRequestMessage(HttpMethod.Get, "/api/driver/me"); anonymous.Headers.Add("X-Test-Anonymous", "true"); Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.SendAsync(anonymous)).StatusCode);
        using var admin = new HttpRequestMessage(HttpMethod.Get, "/api/driver/me"); admin.Headers.Add("X-Test-Role", SecurityRoles.Admin); Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.SendAsync(admin)).StatusCode);
    }

    [Fact]
    public async Task DeclineAndExceptionalTransitionsRequireStructuredConfirmation()
    {
        await using var host = await TestApiHost.StartAsync(); var data = await SeedAsync(host); Authorize(host, data.UserId);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync($"/api/driver/jobs/{data.BookingId}/decline", new DriverDeclineRequest("Other", null))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsJsonAsync($"/api/driver/jobs/{data.BookingId}/decline", new DriverDeclineRequest("Unavailable", "Shift ending"))).StatusCode);
        await using var scope = host.App.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>(); Assert.Single(db.DriverJobDeclines); Assert.Equal(BookingStatus.Confirmed, (await db.Bookings.FindAsync(data.BookingId))!.Status);
    }

    private static void Authorize(TestApiHost host, Guid userId) { host.Client.DefaultRequestHeaders.Add("X-Test-Role", SecurityRoles.Driver); host.Client.DefaultRequestHeaders.Add("X-Test-User", userId.ToString()); }
    private static async Task<(Guid UserId, Guid DriverId, Guid OtherDriverId, Guid VehicleId, Guid BookingId, Guid OtherBookingId)> SeedAsync(TestApiHost host)
    {
        await using var scope = host.App.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>(); var now = DateTimeOffset.UtcNow; var future = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1));
        var vehicle = new Vehicle { Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, RegistrationNumber = "DRV 100", Make = "Mercedes", Model = "Vito", VehicleType = VehicleType.MPV, PassengerCapacity = 6, LuggageCapacity = 6, IsActive = true, MOTExpiry = future, InsuranceExpiry = future, PrivateHireLicenceExpiry = future, CreatedAt = now, UpdatedAt = now };
        var driver = new Driver { Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, FirstName = "Mobile", LastName = "Driver", Email = "driver@example.test", Phone = "07111111111", DriverNumber = "D100", VehicleId = vehicle.Id, IsActive = true, DrivingLicenceExpiry = future, PrivateHireLicenceExpiry = future, DBSExpiry = future, MedicalExpiry = future, CreatedAt = now, UpdatedAt = now };
        var other = new Driver { Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, FirstName = "Other", LastName = "Driver", Email = "otherdriver@example.test", Phone = "07222222222", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var customer = new Customer { Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, FirstName = "Driver", LastName = "Passenger", Email = "passenger@example.test", Phone = "07333333333", IsActive = true, CreatedAt = now, UpdatedAt = now };
        Booking Booking(Guid driverId, string reference) => new() { Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, CustomerId = customer.Id, DriverId = driverId, BookingReference = reference, PickupAddress = "Pickup", Destination = "Destination", PickupDateTime = now.AddHours(1), PassengerCount = 1, LuggageCount = 1, VehicleType = VehicleType.MPV, Status = BookingStatus.Assigned, PaymentStatus = "Pending", TotalFare = 100, CreatedAt = now, UpdatedAt = now };
        var booking = Booking(driver.Id, "LVC-DRV-1"); var otherBooking = Booking(other.Id, "LVC-DRV-2"); var userId = Guid.NewGuid(); var user = new ApplicationUser { Id = userId, CompanyId = LondonVipCompany.Id, DriverId = driver.Id, UserName = "driver@example.test", NormalizedUserName = "DRIVER@EXAMPLE.TEST", Email = "driver@example.test", NormalizedEmail = "DRIVER@EXAMPLE.TEST", EmailConfirmed = true, IsActive = true, SecurityStamp = Guid.NewGuid().ToString(), CreatedAt = now };
        db.AddRange(vehicle, driver, other, customer, booking, otherBooking, user); await db.SaveChangesAsync(); return (userId, driver.Id, other.Id, vehicle.Id, booking.Id, otherBooking.Id);
    }
}
