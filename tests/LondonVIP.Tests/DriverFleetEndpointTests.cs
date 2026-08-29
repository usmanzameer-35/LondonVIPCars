using System.Net;
using System.Net.Http.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.Drivers;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Security;
using LondonVIP.Shared.Vehicles;
using LondonVIP.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LondonVIP.Tests;

public class DriverFleetEndpointTests
{
    [Fact] public async Task AdminDriverCrudIsTenantScopedAndAudited()
    {
        await using var h=await TestApiHost.StartAsync(); var d=await CreateDriver(h,"driver@example.test");
        Assert.Equal(d.Id,(await h.Client.GetFromJsonAsync<List<DriverListItemDto>>("/api/drivers"))!.Single().Id);
        var update=Driver("changed@example.test"); update.FirstName="Changed"; using var put=await h.Client.PutAsJsonAsync($"/api/drivers/{d.Id}",update); Assert.Equal("Changed",(await put.Content.ReadFromJsonAsync<DriverDetailDto>())!.FirstName);
        await using var s=h.App.Services.CreateAsyncScope(); var db=s.ServiceProvider.GetRequiredService<LondonVIPDbContext>(); Assert.Contains(db.SecurityAuditEvents,x=>x.EventType=="DriverCreated"); Assert.Contains(db.SecurityAuditEvents,x=>x.EventType=="DriverUpdated");
    }
    [Fact] public async Task DriverValidationAndDuplicateEmailAreRejected()
    {
        await using var h=await TestApiHost.StartAsync(); Assert.Equal(HttpStatusCode.BadRequest,(await h.Client.PostAsJsonAsync("/api/drivers",new DriverCreateDto())).StatusCode);
        await CreateDriver(h,"Same@Example.Test"); Assert.Equal(HttpStatusCode.BadRequest,(await h.Client.PostAsJsonAsync("/api/drivers",Driver(" same@example.test "))).StatusCode);
    }
    [Fact] public async Task DriverOperationalStatusAndVehicleAssignmentWork()
    {
        await using var h=await TestApiHost.StartAsync(); var v=await CreateVehicle(h,"AB12 CDE"); var d=await CreateDriver(h,"ops@example.test");
        Assert.Equal(HttpStatusCode.OK,(await Patch(h,$"/api/drivers/{d.Id}/vehicle",new DriverVehicleAssignmentDto{VehicleId=v.Id})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Patch(h,$"/api/drivers/{d.Id}/availability",new DriverAvailabilityUpdateDto{AvailabilityStatus=DriverAvailabilityStatus.Busy})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Patch(h,$"/api/drivers/{d.Id}/vehicle",new DriverVehicleAssignmentDto())).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Patch(h,$"/api/drivers/{d.Id}/status",new DriverStatusUpdateDto{IsActive=false})).StatusCode);
        await using var s=h.App.Services.CreateAsyncScope();var db=s.ServiceProvider.GetRequiredService<LondonVIPDbContext>();Assert.Contains(db.SecurityAuditEvents,x=>x.EventType=="DriverVehicleAssigned");Assert.Contains(db.SecurityAuditEvents,x=>x.EventType=="DriverAvailabilityChanged");Assert.Contains(db.SecurityAuditEvents,x=>x.EventType=="DriverVehicleUnassigned");Assert.Contains(db.SecurityAuditEvents,x=>x.EventType=="DriverDeactivated");
    }
    [Fact] public async Task UnsafeVehicleAssignmentsAreBlockedAndExplicitReassignmentWorks()
    {
        await using var h=await TestApiHost.StartAsync();var inactive=await CreateVehicle(h,"ZZ99 ZZZ",false);var first=await CreateDriver(h,"first@example.test");Assert.Equal(HttpStatusCode.Conflict,(await Patch(h,$"/api/drivers/{first.Id}/vehicle",new DriverVehicleAssignmentDto{VehicleId=inactive.Id})).StatusCode);
        var active=await CreateVehicle(h,"XY12 XYZ");var second=await CreateDriver(h,"second@example.test");(await Patch(h,$"/api/drivers/{first.Id}/vehicle",new DriverVehicleAssignmentDto{VehicleId=active.Id})).EnsureSuccessStatusCode();Assert.Equal(HttpStatusCode.Conflict,(await Patch(h,$"/api/drivers/{second.Id}/vehicle",new DriverVehicleAssignmentDto{VehicleId=active.Id})).StatusCode);Assert.Equal(HttpStatusCode.OK,(await Patch(h,$"/api/drivers/{second.Id}/vehicle",new DriverVehicleAssignmentDto{VehicleId=active.Id,Reassign=true})).StatusCode);
    }
    [Fact] public async Task CrossTenantDriverAndVehicleAccessReturn404AndAreAudited()
    {
        await using var h=await TestApiHost.StartAsync();var other=await AddOtherTenantData(h);var current=await CreateDriver(h,"current@example.test");Assert.Equal(HttpStatusCode.NotFound,(await h.Client.GetAsync($"/api/drivers/{other.Driver.Id}")).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await h.Client.GetAsync($"/api/vehicles/{other.Vehicle.Id}")).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await Patch(h,$"/api/drivers/{current.Id}/vehicle",new DriverVehicleAssignmentDto{VehicleId=other.Vehicle.Id})).StatusCode);await using var s=h.App.Services.CreateAsyncScope();var db=s.ServiceProvider.GetRequiredService<LondonVIPDbContext>();Assert.True(db.SecurityAuditEvents.Count(x=>x.EventType=="CrossTenantAccessAttempt")>=2);
    }
    [Theory][InlineData(SecurityRoles.Dispatcher,HttpStatusCode.OK,HttpStatusCode.Forbidden,HttpStatusCode.OK)][InlineData(SecurityRoles.Finance,HttpStatusCode.OK,HttpStatusCode.Forbidden,HttpStatusCode.Forbidden)][InlineData(SecurityRoles.Driver,HttpStatusCode.Forbidden,HttpStatusCode.Forbidden,HttpStatusCode.Forbidden)] public async Task DriverAuthorizationIsLeastPrivilege(string role,HttpStatusCode read,HttpStatusCode write,HttpStatusCode operational)
    {
        await using var h=await TestApiHost.StartAsync();var d=await CreateDriver(h,"roles@example.test");Assert.Equal(read,(await Send(h,HttpMethod.Get,"/api/drivers",role)).StatusCode);Assert.Equal(write,(await Send(h,HttpMethod.Post,"/api/drivers",role,Driver("new@example.test"))).StatusCode);Assert.Equal(operational,(await Send(h,HttpMethod.Patch,$"/api/drivers/{d.Id}/availability",role,new DriverAvailabilityUpdateDto{AvailabilityStatus=DriverAvailabilityStatus.Available})).StatusCode);
    }
    [Fact] public async Task AdminVehicleCrudValidationNormalizationAndAuditWork()
    {
        await using var h=await TestApiHost.StartAsync();Assert.Equal(HttpStatusCode.BadRequest,(await h.Client.PostAsJsonAsync("/api/vehicles",new VehicleCreateDto())).StatusCode);var v=await CreateVehicle(h,"AB12 CDE");Assert.Contains((await h.Client.GetFromJsonAsync<List<VehicleListItemDto>>("/api/vehicles"))!,x=>x.Id==v.Id);Assert.Equal(v.Id,(await h.Client.GetFromJsonAsync<VehicleDetailDto>($"/api/vehicles/{v.Id}"))!.Id);Assert.Equal(HttpStatusCode.BadRequest,(await h.Client.PostAsJsonAsync("/api/vehicles",Vehicle("ab12cde"))).StatusCode);var update=Vehicle("AB12 CDE");update.Model="Updated";Assert.Equal(HttpStatusCode.OK,(await h.Client.PutAsJsonAsync($"/api/vehicles/{v.Id}",update)).StatusCode);Assert.Equal(HttpStatusCode.OK,(await Patch(h,$"/api/vehicles/{v.Id}/status",new VehicleStatusUpdateDto{IsActive=false})).StatusCode);await using var s=h.App.Services.CreateAsyncScope();var db=s.ServiceProvider.GetRequiredService<LondonVIPDbContext>();Assert.Contains(db.SecurityAuditEvents,x=>x.EventType=="VehicleCreated");Assert.Contains(db.SecurityAuditEvents,x=>x.EventType=="VehicleUpdated");Assert.Contains(db.SecurityAuditEvents,x=>x.EventType=="VehicleDeactivated");
    }
    [Theory][InlineData(SecurityRoles.Dispatcher,HttpStatusCode.OK,HttpStatusCode.Forbidden)][InlineData(SecurityRoles.Finance,HttpStatusCode.OK,HttpStatusCode.Forbidden)][InlineData(SecurityRoles.Driver,HttpStatusCode.Forbidden,HttpStatusCode.Forbidden)] public async Task VehicleAuthorizationIsLeastPrivilege(string role,HttpStatusCode read,HttpStatusCode write)
    {await using var h=await TestApiHost.StartAsync();Assert.Equal(read,(await Send(h,HttpMethod.Get,"/api/vehicles",role)).StatusCode);Assert.Equal(write,(await Send(h,HttpMethod.Post,"/api/vehicles",role,Vehicle("ROLE 1"))).StatusCode);}

    private static DriverUpdateDto Driver(string email)=>new(){FirstName="Alex",LastName="Driver",Phone="07123456789",Email=email,IsActive=true,AvailabilityStatus=DriverAvailabilityStatus.Available};
    private static VehicleUpdateDto Vehicle(string registration)=>new(){RegistrationNumber=registration,Make="Mercedes",Model="E Class",VehicleType=VehicleType.Saloon,PassengerCapacity=4,LuggageCapacity=2,IsActive=true};
    private static async Task<DriverDetailDto> CreateDriver(TestApiHost h,string email){using var r=await h.Client.PostAsJsonAsync("/api/drivers",Driver(email));r.EnsureSuccessStatusCode();return(await r.Content.ReadFromJsonAsync<DriverDetailDto>())!;}
    private static async Task<VehicleDetailDto> CreateVehicle(TestApiHost h,string reg,bool active=true){var dto=Vehicle(reg);dto.IsActive=active;using var r=await h.Client.PostAsJsonAsync("/api/vehicles",dto);r.EnsureSuccessStatusCode();return(await r.Content.ReadFromJsonAsync<VehicleDetailDto>())!;}
    private static async Task<(Driver Driver,Vehicle Vehicle)> AddOtherTenantData(TestApiHost h){var id=Guid.NewGuid();var now=DateTimeOffset.UtcNow;var c=new Company{Id=id,TradingName="Other",LegalName="Other",Slug=$"other-{id:N}",Email="",Phone="",WebsiteUrl="",AddressLine1="",AddressLine2="",City="London",Postcode="",Country="UK",TimeZone="Europe/London",CurrencyCode="GBP",IsActive=true,CreatedAt=now,UpdatedAt=now};var v=new Vehicle{Id=Guid.NewGuid(),CompanyId=id,RegistrationNumber="OTHER1",Make="Other",Model="Car",PassengerCapacity=4,LuggageCapacity=2,IsActive=true,CreatedAt=now,UpdatedAt=now};var d=new Driver{Id=Guid.NewGuid(),CompanyId=id,FirstName="Other",LastName="Driver",Phone="07123456789",Email="other@tenant.test",IsActive=true,CreatedAt=now,UpdatedAt=now};await using var s=h.App.Services.CreateAsyncScope();var db=s.ServiceProvider.GetRequiredService<LondonVIPDbContext>();db.Add(c);db.Add(v);db.Add(d);await db.SaveChangesAsync();return(d,v);}
    private static Task<HttpResponseMessage> Patch<T>(TestApiHost h,string uri,T body)=>h.Client.SendAsync(new HttpRequestMessage(HttpMethod.Patch,uri){Content=JsonContent.Create(body)});
    private static Task<HttpResponseMessage> Send<T>(TestApiHost h,HttpMethod method,string uri,string role,T? body=default){var r=new HttpRequestMessage(method,uri);r.Headers.Add("X-Test-Role",role);if(body is not null)r.Content=JsonContent.Create(body);return h.Client.SendAsync(r);}private static Task<HttpResponseMessage> Send(TestApiHost h,HttpMethod method,string uri,string role)=>Send<object>(h,method,uri,role);
}
