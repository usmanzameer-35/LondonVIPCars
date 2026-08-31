using System.Net;
using System.Net.Http.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.Dispatch;
using LondonVIP.Shared.Models;
using LondonVIP.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace LondonVIP.Tests;

public class DispatchEndpointTests
{
    [Fact]
    public async Task DispatchCentre_DashboardSearchFilteringAndAuditUseTenantData()
    {
        await WithDispatchDataAsync(async (host, data) =>
        {
            var dashboard = await host.Client.GetFromJsonAsync<DispatchDashboardDto>("/api/dispatch/dashboard");
            Assert.NotNull(dashboard); Assert.Equal(1, dashboard.Kpis.BookingsWaiting); Assert.Contains(dashboard.WaitingBookings, x => x.BookingId == data.Booking.Id);
            var page = await host.Client.GetFromJsonAsync<DispatchPageDto<DispatchBoardItemDto>>("/api/dispatch/bookings?status=Confirmed&page=1&pageSize=10");
            Assert.NotNull(page); Assert.Equal(1, page.Total); Assert.Single(page.Items);
            var search = await host.Client.GetFromJsonAsync<List<DispatchSearchResultDto>>($"/api/dispatch/search?q={data.Booking.BookingReference}");
            Assert.Contains(search!, x => x.Type == "Booking" && x.Id == data.Booking.Id);
            await using var scope=host.App.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
            Assert.Contains(db.SecurityAuditEvents,x=>x.EventType=="DispatchViewed"&&x.CompanyId==data.CompanyId);
        });
    }

    [Fact]
    public async Task StrictAssignmentDetectsConflictAndRecommendationsExcludeOccupiedDriver()
    {
        await WithDispatchDataAsync(async (host, data) =>
        {
            await AddBookingAsync(host,data,BookingStatus.Assigned,data.ActiveDriver.Id);
            using var response=await host.Client.PostAsJsonAsync($"/api/dispatch/bookings/{data.Booking.Id}/assign",new AssignDriverRequest{DriverId=data.ActiveDriver.Id});
            Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
            var recommendations=await host.Client.GetFromJsonAsync<List<DriverRecommendationDto>>($"/api/dispatch/bookings/{data.Booking.Id}/recommendations");
            Assert.DoesNotContain(recommendations!,x=>x.DriverId==data.ActiveDriver.Id);
        });
    }

    [Fact]
    public async Task DispatchCentre_IsTenantSafeAuthorizedAndHandlesEmptyDatabase()
    {
        await using var host=await TestApiHost.StartAsync();
        var dashboard=await host.Client.GetFromJsonAsync<DispatchDashboardDto>("/api/dispatch/dashboard");Assert.NotNull(dashboard);Assert.Equal(0,dashboard.Kpis.BookingsWaiting);
        using var request=new HttpRequestMessage(HttpMethod.Get,"/api/dispatch/dashboard");request.Headers.Add("X-Test-Anonymous","true");Assert.Equal(HttpStatusCode.Unauthorized,(await host.Client.SendAsync(request)).StatusCode);
        var other=await AddOtherTenantAsync(host);Assert.Equal(HttpStatusCode.NotFound,(await host.Client.GetAsync($"/api/dispatch/bookings/{other.Booking.Id}")).StatusCode);
    }

    [Fact]
    public async Task DispatchList_ReturnsOnlyCurrentTenantOperationalBookings()
    {
        await WithDispatchDataAsync(async (host, data) =>
        {
            await AddBookingAsync(host, data, BookingStatus.Completed);
            var other = await AddOtherTenantAsync(host);
            var jobs = await host.Client.GetFromJsonAsync<List<DispatchBoardItemDto>>("/api/dispatch");

            Assert.NotNull(jobs);
            Assert.Contains(jobs, item => item.BookingId == data.Booking.Id);
            Assert.DoesNotContain(jobs, item => item.Status is BookingStatus.Completed or BookingStatus.Cancelled);
            Assert.DoesNotContain(jobs, item => item.BookingId == other.Booking.Id);
        });
    }

    [Fact]
    public async Task UnassignedList_ReturnsOnlyConfirmedBookingsWithoutDriver()
    {
        await WithDispatchDataAsync(async (host, data) =>
        {
            var assigned = await AddBookingAsync(host, data, BookingStatus.Assigned, data.ActiveDriver.Id);
            var jobs = await host.Client.GetFromJsonAsync<List<DispatchBoardItemDto>>("/api/dispatch/unassigned");

            var job = Assert.Single(jobs!);
            Assert.Equal(data.Booking.Id, job.BookingId);
            Assert.DoesNotContain(jobs!, item => item.BookingId == assigned.Id);
        });
    }

    [Fact]
    public async Task DriversList_ReturnsActiveCurrentTenantDriversWithVehicle()
    {
        await WithDispatchDataAsync(async (host, data) =>
        {
            await AddOtherTenantAsync(host);
            var drivers = await host.Client.GetFromJsonAsync<List<DriverAvailabilityDto>>("/api/dispatch/drivers");

            var driver = Assert.Single(drivers!);
            Assert.Equal(data.ActiveDriver.Id, driver.DriverId);
            Assert.Equal(data.Vehicle.RegistrationNumber, driver.RegistrationNumber);
            Assert.DoesNotContain(drivers!, item => item.DriverId == data.InactiveDriver.Id);
        });
    }

    [Fact]
    public async Task AssignAndReassignDriver_UpdatesBookingAndStatus()
    {
        await WithDispatchDataAsync(async (host, data) =>
        {
            var second = await AddDriverAsync(host, data.CompanyId, true, null, "Second");
            var firstResult = await PatchAsync(host.Client, $"/api/dispatch/{data.Booking.Id}/assign", new AssignDriverRequest { DriverId = data.ActiveDriver.Id });
            Assert.Equal(HttpStatusCode.OK, firstResult.StatusCode);
            var first = await firstResult.Content.ReadFromJsonAsync<DispatchBoardItemDto>();
            Assert.Equal(BookingStatus.Assigned, first?.Status);
            Assert.Equal(data.ActiveDriver.Id, first?.DriverId);

            var secondResult = await PatchAsync(host.Client, $"/api/dispatch/{data.Booking.Id}/assign", new AssignDriverRequest { DriverId = second.Id });
            Assert.Equal(HttpStatusCode.OK, secondResult.StatusCode);
            var reassigned = await secondResult.Content.ReadFromJsonAsync<DispatchBoardItemDto>();
            Assert.Equal(second.Id, reassigned?.DriverId);
        });
    }

    [Fact]
    public async Task UnassignDriver_ReturnsBookingToConfirmed()
    {
        await WithDispatchDataAsync(async (host, data) =>
        {
            using var assigned = await PatchAsync(host.Client, $"/api/dispatch/{data.Booking.Id}/assign", new AssignDriverRequest { DriverId = data.ActiveDriver.Id });
            assigned.EnsureSuccessStatusCode();
            using var response = await PatchAsync(host.Client, $"/api/dispatch/{data.Booking.Id}/unassign", new UnassignDriverRequest());
            var result = await response.Content.ReadFromJsonAsync<DispatchBoardItemDto>();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(BookingStatus.Confirmed, result?.Status);
            Assert.Null(result?.DriverId);
        });
    }

    [Fact]
    public async Task StatusProgression_MovesAssignedJourneyToCompletion()
    {
        await WithDispatchDataAsync(async (host, data) =>
        {
            using var assigned = await PatchAsync(host.Client, $"/api/dispatch/{data.Booking.Id}/assign", new AssignDriverRequest { DriverId = data.ActiveDriver.Id });
            assigned.EnsureSuccessStatusCode();
            foreach (var status in new[] { BookingStatus.DriverEnRoute, BookingStatus.PassengerOnBoard, BookingStatus.Completed })
            {
                using var response = await PatchAsync(host.Client, $"/api/dispatch/{data.Booking.Id}/status", new DispatchStatusUpdateDto { Status = status });
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            var jobs = await host.Client.GetFromJsonAsync<List<DispatchBoardItemDto>>("/api/dispatch");
            Assert.DoesNotContain(jobs!, item => item.BookingId == data.Booking.Id);
        });
    }

    [Fact]
    public async Task OperationalActions_MoveJourneyThroughArrivalToCompletionAndAuditEvents()
    {
        await WithDispatchDataAsync(async (host, data) =>
        {
            using var assigned = await PatchAsync(host.Client, $"/api/dispatch/{data.Booking.Id}/assign", new AssignDriverRequest { DriverId = data.ActiveDriver.Id });
            assigned.EnsureSuccessStatusCode();
            foreach (var action in new[] { "accept", "start-navigation", "arrive", "passenger-onboard", "complete" })
            {
                using var response = await host.Client.PostAsync($"/api/bookings/{data.Booking.Id}/{action}", null);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
            await using var scope = host.App.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
            Assert.Equal(BookingStatus.Completed, await db.Bookings.Where(x => x.Id == data.Booking.Id).Select(x => x.Status).SingleAsync());
            Assert.Contains(db.SecurityAuditEvents, x => x.EventType == "DriverArrived" && x.ResourceIdentifier == data.Booking.Id.ToString());
            Assert.Contains(db.SecurityAuditEvents, x => x.EventType == "BookingCompleted" && x.ResourceIdentifier == data.Booking.Id.ToString());
        });
    }

    [Fact]
    public async Task NoShowAndUnableToComplete_RequireEligibleOperationalStates()
    {
        await WithDispatchDataAsync(async (host, data) =>
        {
            Assert.Equal(HttpStatusCode.Conflict, (await host.Client.PostAsync($"/api/bookings/{data.Booking.Id}/no-show", null)).StatusCode);
            var arrived = await AddBookingAsync(host, data, BookingStatus.DriverArrived, data.ActiveDriver.Id);
            Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsync($"/api/bookings/{arrived.Id}/no-show", null)).StatusCode);
            var onboard = await AddBookingAsync(host, data, BookingStatus.PassengerOnBoard, data.ActiveDriver.Id);
            Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsync($"/api/bookings/{onboard.Id}/unable-to-complete", null)).StatusCode);
        });
    }

    [Fact]
    public async Task InvalidAssignmentState_ReturnsConflict()
    {
        await WithDispatchDataAsync(async (host, data) =>
        {
            var pending = await AddBookingAsync(host, data, BookingStatus.Pending);
            using var response = await PatchAsync(host.Client, $"/api/dispatch/{pending.Id}/assign", new AssignDriverRequest { DriverId = data.ActiveDriver.Id });
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        });
    }

    [Fact]
    public async Task CrossTenantBooking_ReturnsNotFound()
    {
        await WithDispatchDataAsync(async (host, data) =>
        {
            var other = await AddOtherTenantAsync(host);
            using var response = await PatchAsync(host.Client, $"/api/dispatch/{other.Booking.Id}/assign", new AssignDriverRequest { DriverId = data.ActiveDriver.Id });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        });
    }

    [Fact]
    public async Task CrossTenantDriver_ReturnsNotFound()
    {
        await WithDispatchDataAsync(async (host, data) =>
        {
            var other = await AddOtherTenantAsync(host);
            using var response = await PatchAsync(host.Client, $"/api/dispatch/{data.Booking.Id}/assign", new AssignDriverRequest { DriverId = other.Driver.Id });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        });
    }

    [Fact]
    public async Task InactiveDriver_ReturnsConflict()
    {
        await WithDispatchDataAsync(async (host, data) =>
        {
            using var response = await PatchAsync(host.Client, $"/api/dispatch/{data.Booking.Id}/assign", new AssignDriverRequest { DriverId = data.InactiveDriver.Id });
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        });
    }

    private static async Task WithDispatchDataAsync(Func<TestApiHost, DispatchData, Task> test)
    {
        await using var host = await TestApiHost.StartAsync();
        var companyId = LondonVipCompany.Id;
        var customer = NewCustomer(companyId, "Current");
        var vehicle = NewVehicle(companyId, "LVC26VIP");
        var active = NewDriver(companyId, true, vehicle.Id, "Active");
        var inactive = NewDriver(companyId, false, null, "Inactive");
        var booking = NewBooking(companyId, customer.Id, BookingStatus.Confirmed);
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
            db.AddRange(customer, vehicle, active, inactive, booking);
            await db.SaveChangesAsync();
        }
        await test(host, new DispatchData(companyId, customer, vehicle, active, inactive, booking));
    }

    private static async Task<Booking> AddBookingAsync(TestApiHost host, DispatchData data, BookingStatus status, Guid? driverId = null)
    {
        var booking = NewBooking(data.CompanyId, data.Customer.Id, status, driverId);
        await using var scope = host.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        db.Bookings.Add(booking); await db.SaveChangesAsync(); return booking;
    }

    private static async Task<Driver> AddDriverAsync(TestApiHost host, Guid companyId, bool active, Guid? vehicleId, string name)
    {
        var driver = NewDriver(companyId, active, vehicleId, name);
        await using var scope = host.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        db.Drivers.Add(driver); await db.SaveChangesAsync(); return driver;
    }

    private static async Task<OtherTenantData> AddOtherTenantAsync(TestApiHost host)
    {
        var companyId = Guid.NewGuid();
        var company = new Company { Id = companyId, TradingName = "Other Cars", LegalName = "Other Cars", Slug = $"other-{companyId:N}", Email = "other@example.test", Phone = "000", WebsiteUrl = "", AddressLine1 = "Test", AddressLine2 = "", City = "London", Postcode = "TEST", Country = "UK", TimeZone = "Europe/London", CurrencyCode = "GBP", IsActive = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var customer = NewCustomer(companyId, "Other");
        var driver = NewDriver(companyId, true, null, "Other");
        var booking = NewBooking(companyId, customer.Id, BookingStatus.Confirmed);
        await using var scope = host.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        db.AddRange(company, customer, driver, booking); await db.SaveChangesAsync();
        return new OtherTenantData(company, driver, booking);
    }

    private static Customer NewCustomer(Guid companyId, string name) => new() { Id = Guid.NewGuid(), CompanyId = companyId, FirstName = name, LastName = "Passenger", Email = $"{Guid.NewGuid():N}@example.test", Phone = "000", CreatedAt = DateTimeOffset.UtcNow, IsActive = true };
    private static Vehicle NewVehicle(Guid companyId, string registration) => new() { Id = Guid.NewGuid(), CompanyId = companyId, RegistrationNumber = registration, Make = "Mercedes", Model = "E-Class", VehicleType = VehicleType.Saloon, PassengerCapacity = 4, LuggageCapacity = 2, IsActive = true };
    private static Driver NewDriver(Guid companyId, bool active, Guid? vehicleId, string name) => new() { Id = Guid.NewGuid(), CompanyId = companyId, FirstName = name, LastName = "Driver", Email = $"{Guid.NewGuid():N}@example.test", Phone = "000", VehicleId = vehicleId, IsActive = active, AvailabilityStatus = active ? DriverAvailabilityStatus.Available : DriverAvailabilityStatus.Offline };
    private static Booking NewBooking(Guid companyId, Guid customerId, BookingStatus status, Guid? driverId = null) => new() { Id = Guid.NewGuid(), BookingReference = $"LVC-{Guid.NewGuid():N}"[..20], CompanyId = companyId, CustomerId = customerId, PickupAddress = "Heathrow Terminal 5", Destination = "Mayfair", PickupDateTime = DateTimeOffset.UtcNow.AddMinutes(45), PassengerCount = 2, LuggageCount = 2, VehicleType = VehicleType.Saloon, BaseFare = 50m, Extras = 10m, TotalFare = 60m, DriverId = driverId, Status = status, PaymentStatus = "Pending", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };

    private static Task<HttpResponseMessage> PatchAsync<T>(HttpClient client, string uri, T body) => client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, uri) { Content = JsonContent.Create(body) });
    private sealed record DispatchData(Guid CompanyId, Customer Customer, Vehicle Vehicle, Driver ActiveDriver, Driver InactiveDriver, Booking Booking);
    private sealed record OtherTenantData(Company Company, Driver Driver, Booking Booking);
}
