using System.Net;
using System.Net.Http.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.Drivers;
using LondonVIP.Shared.Models;
using LondonVIP.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LondonVIP.Tests;

public sealed class DriverDashboardEndpointTests
{
    [Fact]
    public async Task Dashboard_ReturnsOnlySelectedTenantDriverWorkload()
    {
        await using var host=await TestApiHost.StartAsync();var driver=NewDriver(LondonVipCompany.Id);var customer=NewCustomer(LondonVipCompany.Id);
        var active=NewBooking(LondonVipCompany.Id,customer.Id,driver.Id,BookingStatus.DriverEnRoute,DateTimeOffset.UtcNow.AddHours(1));
        var completed=NewBooking(LondonVipCompany.Id,customer.Id,driver.Id,BookingStatus.Completed,DateTimeOffset.UtcNow);
        await using(var scope=host.App.Services.CreateAsyncScope()){var db=scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();db.AddRange(driver,customer,active,completed);await db.SaveChangesAsync();}
        var dashboard=await host.Client.GetFromJsonAsync<DriverDashboardDto>($"/api/drivers/{driver.Id}/dashboard");
        Assert.NotNull(dashboard);Assert.Equal(driver.Id,dashboard.DriverId);Assert.Equal(active.Id,dashboard.CurrentJob?.BookingId);Assert.Equal(1,dashboard.CompletedToday);Assert.Contains(dashboard.TodaysJobs,x=>x.BookingId==completed.Id);
    }

    [Fact]
    public async Task Dashboard_CrossTenantDriverReturnsNotFound()
    {
        await using var host=await TestApiHost.StartAsync();var companyId=Guid.NewGuid();var driver=NewDriver(companyId);
        await using(var scope=host.App.Services.CreateAsyncScope()){var db=scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();db.Companies.Add(new Company{Id=companyId,TradingName="Other",LegalName="Other",Slug=$"other-{companyId:N}",CurrencyCode="GBP",TimeZone="Europe/London",IsActive=true,CreatedAt=DateTimeOffset.UtcNow,UpdatedAt=DateTimeOffset.UtcNow});db.Drivers.Add(driver);await db.SaveChangesAsync();}
        Assert.Equal(HttpStatusCode.NotFound,(await host.Client.GetAsync($"/api/drivers/{driver.Id}/dashboard")).StatusCode);
    }

    private static Driver NewDriver(Guid companyId)=>new(){Id=Guid.NewGuid(),CompanyId=companyId,FirstName="Dashboard",LastName="Driver",Email=$"{Guid.NewGuid():N}@test.local",Phone="000",IsActive=true,AvailabilityStatus=DriverAvailabilityStatus.Available};
    private static Customer NewCustomer(Guid companyId)=>new(){Id=Guid.NewGuid(),CompanyId=companyId,FirstName="Dashboard",LastName="Passenger",Email=$"{Guid.NewGuid():N}@test.local",Phone="000",IsActive=true,CreatedAt=DateTimeOffset.UtcNow};
    private static Booking NewBooking(Guid companyId,Guid customerId,Guid driverId,BookingStatus status,DateTimeOffset pickup)=>new(){Id=Guid.NewGuid(),CompanyId=companyId,CustomerId=customerId,DriverId=driverId,BookingReference=$"LVC-{Guid.NewGuid():N}"[..20],PickupAddress="Pickup",Destination="Destination",PickupDateTime=pickup,PassengerCount=1,VehicleType=VehicleType.Saloon,Status=status,PaymentStatus="Pending",CreatedAt=DateTimeOffset.UtcNow,UpdatedAt=DateTimeOffset.UtcNow};
}
