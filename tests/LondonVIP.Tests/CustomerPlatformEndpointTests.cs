using System.Net;
using System.Net.Http.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Security;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.CustomerPortal;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Security;
using LondonVIP.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LondonVIP.Tests;

public sealed class CustomerPlatformEndpointTests
{
    [Fact]
    public async Task RegistrationCreatesLinkedTenantCustomerAndIdentity()
    {
        await using var host=await TestApiHost.StartAsync();var request=new CustomerRegistrationRequest("london-vip-cars","Portal","Customer","portal-new@example.test","07123456789","SecurePassword!123");using var response=await host.Client.PostAsJsonAsync("/api/customer-auth/register",request);Assert.Equal(HttpStatusCode.Created,response.StatusCode);await using var scope=host.App.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();var user=await db.Users.SingleAsync(x=>x.Email==request.Email);Assert.NotNull(user.CustomerId);Assert.Equal(LondonVipCompany.Id,user.CompanyId);Assert.Contains(db.SecurityAuditEvents,x=>x.EventType=="CustomerRegistered");Assert.Contains(db.Notifications,x=>x.TemplateName=="customer-email-verification");
    }

    [Fact]
    public async Task CustomerOwnsProfileAddressesBookingsAndPaymentIntentBoundary()
    {
        await using var host=await TestApiHost.StartAsync();var data=await SeedAsync(host);host.Client.DefaultRequestHeaders.Add("X-Test-Role",SecurityRoles.Customer);host.Client.DefaultRequestHeaders.Add("X-Test-User",data.UserId.ToString());Assert.Equal(HttpStatusCode.OK,(await host.Client.GetAsync("/api/customer/profile")).StatusCode);using var address=await host.Client.PostAsJsonAsync("/api/customer/addresses",new CustomerAddressRequest("Home","1 London Road",null,"London","W6 9AA",true,true));Assert.Equal(HttpStatusCode.Created,address.StatusCode);Assert.Single((await host.Client.GetFromJsonAsync<List<CustomerAddressDto>>("/api/customer/addresses"))!);Assert.Equal(HttpStatusCode.NotFound,(await host.Client.PutAsJsonAsync($"/api/customer/bookings/{data.OtherBookingId}",new CustomerBookingAmendRequest(DateTimeOffset.UtcNow.AddDays(2),"A","B",1,0,null))).StatusCode);Assert.Equal(HttpStatusCode.NoContent,(await host.Client.PostAsJsonAsync($"/api/customer/bookings/{data.BookingId}/cancel",new CustomerBookingCancelRequest("Plans changed"))).StatusCode);using var payment=await host.Client.PostAsJsonAsync("/api/customer/payments/intents",new CustomerPaymentIntentRequest(data.InvoiceId,50,"Card","retry-safe-1"));Assert.Equal(HttpStatusCode.ServiceUnavailable,payment.StatusCode);using var anonymous=new HttpRequestMessage(HttpMethod.Get,"/api/customer/profile");anonymous.Headers.Add("X-Test-Anonymous","true");Assert.Equal(HttpStatusCode.Unauthorized,(await host.Client.SendAsync(anonymous)).StatusCode);
    }

    private static async Task<(Guid UserId,Guid BookingId,Guid OtherBookingId,Guid InvoiceId)>SeedAsync(TestApiHost host){await using var scope=host.App.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();var now=DateTimeOffset.UtcNow;var c=new Customer{Id=Guid.NewGuid(),CompanyId=LondonVipCompany.Id,FirstName="Self",LastName="Service",Email="self@example.test",Phone="07111111111",IsActive=true,CreatedAt=now,UpdatedAt=now};var other=new Customer{Id=Guid.NewGuid(),CompanyId=LondonVipCompany.Id,FirstName="Other",LastName="Customer",Email="other-customer@example.test",Phone="07222222222",IsActive=true,CreatedAt=now,UpdatedAt=now};Booking B(Customer customer,string reference)=>new(){Id=Guid.NewGuid(),CompanyId=LondonVipCompany.Id,CustomerId=customer.Id,BookingReference=reference,PickupAddress="Pickup",Destination="Destination",PickupDateTime=now.AddDays(1),PassengerCount=1,VehicleType=VehicleType.Saloon,Status=BookingStatus.Confirmed,PaymentStatus="Pending",TotalFare=50,CreatedAt=now,UpdatedAt=now};var b=B(c,"LVC-CUST-1");var ob=B(other,"LVC-CUST-2");var invoice=new Invoice{Id=Guid.NewGuid(),CompanyId=LondonVipCompany.Id,CustomerId=c.Id,InvoiceNumber="INV-CUST-1",InvoiceDate=now,DueDate=now.AddDays(7),Status=InvoiceStatus.Issued,Subtotal=50,TotalAmount=50,BalanceDue=50,CreatedAt=now,UpdatedAt=now};var uid=Guid.NewGuid();var user=new ApplicationUser{Id=uid,CompanyId=LondonVipCompany.Id,CustomerId=c.Id,UserName=c.Email,NormalizedUserName=c.Email.ToUpperInvariant(),Email=c.Email,NormalizedEmail=c.Email.ToUpperInvariant(),EmailConfirmed=true,IsActive=true,SecurityStamp=Guid.NewGuid().ToString(),CreatedAt=now};db.AddRange(c,other,b,ob,invoice,user);await db.SaveChangesAsync();return(uid,b.Id,ob.Id,invoice.Id);}
}
