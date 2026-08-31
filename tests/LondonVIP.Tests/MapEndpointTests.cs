using System.Net;
using System.Net.Http.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.Maps;
using LondonVIP.Shared.Models;
using LondonVIP.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LondonVIP.Tests;

public sealed class MapEndpointTests
{
    [Fact]
    public async Task LocationPublishingFeedsLiveMapHistoryAndCustomerTracking()
    {
        await using var host = await TestApiHost.StartAsync();
        var data = await SeedJourneyAsync(host);
        var update = new DriverLocationUpdateDto(data.DriverId, data.BookingId, data.VehicleId, 51.5074, -0.1278, 180, 22, 5, DateTimeOffset.UtcNow);

        using var published = await host.Client.PostAsJsonAsync("/api/maps/locations", update);
        Assert.Equal(HttpStatusCode.Accepted, published.StatusCode);
        var location = await published.Content.ReadFromJsonAsync<DriverLocationDto>();
        Assert.NotNull(location);
        Assert.Equal(data.BookingId, location.BookingId);

        var live = await host.Client.GetFromJsonAsync<LiveMapDto>("/api/maps");
        Assert.NotNull(live);
        Assert.Contains(live.Drivers, x => x.DriverId == data.DriverId && x.IsOnline);
        var history = await host.Client.GetFromJsonAsync<List<DriverLocationDto>>($"/api/maps/journeys/{data.BookingId}/history");
        Assert.Single(history!);

        using var linkResponse = await host.Client.PostAsync($"/api/maps/journeys/{data.BookingId}/tracking-link", null);
        Assert.Equal(HttpStatusCode.Created, linkResponse.StatusCode);
        var link = await linkResponse.Content.ReadFromJsonAsync<TrackingLinkDto>();
        Assert.NotNull(link);
        var tracking = await host.Client.GetFromJsonAsync<CustomerTrackingDto>($"/api/tracking/{link.Token}");
        Assert.NotNull(tracking);
        Assert.Equal(data.BookingId, tracking.BookingId);
        Assert.Equal(data.DriverId, tracking.DriverLocation?.DriverId);
    }

    [Fact]
    public async Task RoutesSnapshotsAndGeofencesHandleSpatialData()
    {
        await using var host = await TestApiHost.StartAsync();
        var data = await SeedJourneyAsync(host);
        var route = new RouteRequest(new(51.4700, -0.4543), new(51.5074, -0.1278), [new(51.4890, -0.3000)], AvoidTolls: true);
        var calculated = await (await host.Client.PostAsJsonAsync("/api/maps/routes", route)).Content.ReadFromJsonAsync<RouteResult>();
        Assert.NotNull(calculated);
        Assert.True(calculated.Success);
        Assert.True(calculated.DistanceMiles > 0);

        using var snapshotResponse = await host.Client.PostAsJsonAsync($"/api/maps/journeys/{data.BookingId}/snapshot", route);
        Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);
        var snapshot = await snapshotResponse.Content.ReadFromJsonAsync<JourneySnapshotDto>();
        Assert.NotNull(snapshot);
        Assert.Equal(data.BookingId, snapshot.BookingId);

        await using var scope = host.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        Assert.Single(await db.JourneySnapshots.Where(x => x.BookingId == data.BookingId).ToListAsync());
    }

    [Fact]
    public async Task MapsAreAuthorizedValidatedAndTenantIsolated()
    {
        await using var host = await TestApiHost.StartAsync();
        var data = await SeedJourneyAsync(host);
        using var anonymous = new HttpRequestMessage(HttpMethod.Get, "/api/maps");
        anonymous.Headers.Add("X-Test-Anonymous", "true");
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.SendAsync(anonymous)).StatusCode);

        var invalid = new DriverLocationUpdateDto(data.DriverId, data.BookingId, data.VehicleId, 100, -0.1, null, null, null, DateTimeOffset.UtcNow);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/api/maps/locations", invalid)).StatusCode);

        var otherCompany = Guid.NewGuid();
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
            db.Companies.Add(NewCompany(otherCompany));
            db.Drivers.Add(new Driver { Id = Guid.NewGuid(), CompanyId = otherCompany, FirstName = "Other", LastName = "Driver", Email = "other@example.test", Phone = "07000000000", IsActive = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
            var otherDriver = await db.Drivers.SingleAsync(x => x.CompanyId == otherCompany);
            var crossTenant = invalid with { DriverId = otherDriver.Id, Latitude = 51.5 };
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/api/maps/locations", crossTenant)).StatusCode);
        }
        var map = await host.Client.GetFromJsonAsync<LiveMapDto>("/api/maps");
        Assert.DoesNotContain(map!.Drivers, x => x.DriverName == "Other Driver");
    }

    private static async Task<(Guid DriverId, Guid VehicleId, Guid BookingId)> SeedJourneyAsync(TestApiHost host)
    {
        await using var scope = host.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        var now = DateTimeOffset.UtcNow;
        var customer = new Customer { Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, FirstName = "Map", LastName = "Customer", Email = "map@example.test", Phone = "07111111111", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var vehicle = new Vehicle { Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, RegistrationNumber = "MAP 1", Make = "Mercedes", Model = "E-Class", VehicleType = VehicleType.Saloon, PassengerCapacity = 4, LuggageCapacity = 2, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var driver = new Driver { Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, FirstName = "Live", LastName = "Driver", Email = "live@example.test", Phone = "07222222222", VehicleId = vehicle.Id, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var booking = new Booking { Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, CustomerId = customer.Id, DriverId = driver.Id, BookingReference = "LVC-MAP-1", PickupAddress = "Heathrow Airport", Destination = "London", PickupDateTime = now.AddMinutes(30), PassengerCount = 1, VehicleType = VehicleType.Saloon, Status = BookingStatus.Assigned, PaymentStatus = "Pending", TotalFare = 50, CreatedAt = now, UpdatedAt = now };
        db.AddRange(customer, vehicle, driver, booking);
        await db.SaveChangesAsync();
        return (driver.Id, vehicle.Id, booking.Id);
    }

    private static Company NewCompany(Guid id) => new() { Id = id, TradingName = "Other Cars", LegalName = "Other Cars Ltd", Slug = $"other-{id:N}", Email = "office@other.test", Phone = "07000000000", WebsiteUrl = "", AddressLine1 = "1 Road", AddressLine2 = "", City = "London", Postcode = "SW1A 1AA", Country = "GB", TimeZone = "Europe/London", CurrencyCode = "GBP", IsActive = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
}
