using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.Growth;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Security;
using LondonVIP.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LondonVIP.Tests;

public sealed class GrowthEndpointTests
{
    [Fact]
    public async Task PromotionValidationEnforcesEligibilityLimitsAndTenantIsolation()
    {
        await using var host=await TestApiHost.StartAsync();
        var request=new PromotionRequest("WELCOME10","First journey",DiscountKind.Percentage,10,20,50,null,"Heathrow","London",true,false,false,false,100,1,DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(30));
        using var created=await host.Client.PostAsJsonAsync("/api/growth/promotions",request);Assert.Equal(HttpStatusCode.Created,created.StatusCode);
        var valid=await (await host.Client.PostAsJsonAsync("/api/growth/promotions/validate",new PromotionContext("welcome10",100,null,null,null,"Heathrow Terminal 5","London",0))).Content.ReadFromJsonAsync<PromotionValidationResult>();
        Assert.NotNull(valid);Assert.True(valid.IsValid);Assert.Equal(10m,valid.Discount);
        var returning=await (await host.Client.PostAsJsonAsync("/api/growth/promotions/validate",new PromotionContext("WELCOME10",100,null,null,null,"Heathrow","London",1))).Content.ReadFromJsonAsync<PromotionValidationResult>();Assert.False(returning!.IsValid);
        await using var scope=host.App.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();var other=Guid.NewGuid();db.Companies.Add(Company(other));db.Promotions.Add(new Promotion{Id=Guid.NewGuid(),CompanyId=other,Code="OTHER",Name="Other tenant",Kind=DiscountKind.Fixed,Value=50,EffectiveFrom=DateTimeOffset.UtcNow.AddDays(-1),IsActive=true,CreatedAt=DateTimeOffset.UtcNow});await db.SaveChangesAsync();
        var rows=await host.Client.GetFromJsonAsync<List<JsonElement>>("/api/growth/promotions");var row=Assert.Single(rows!);Assert.Equal("WELCOME10",row.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ReferralAndLoyaltyLifecycleArePersistedAndAudited()
    {
        await using var host=await TestApiHost.StartAsync();Guid customerId;
        await using(var scope=host.App.Services.CreateAsyncScope()){var db=scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();customerId=Guid.NewGuid();db.Customers.Add(new Customer{Id=customerId,CompanyId=LondonVipCompany.Id,FirstName="Grace",LastName="Hopper",Email="grace@example.test",Phone="07700900001",IsActive=true,CreatedAt=DateTimeOffset.UtcNow,UpdatedAt=DateTimeOffset.UtcNow});await db.SaveChangesAsync();}
        using var created=await host.Client.PostAsJsonAsync("/api/growth/referrals",new ReferralRequest("Customer",customerId,25));Assert.Equal(HttpStatusCode.Created,created.StatusCode);var referral=await created.Content.ReadFromJsonAsync<ReferralDto>();Assert.NotNull(referral);Assert.StartsWith("REF-",referral.Code);
        Assert.Equal(HttpStatusCode.OK,(await host.Client.PostAsync($"/api/growth/referrals/{referral.Id}/qualify/{customerId}",null)).StatusCode);
        using var points=await host.Client.PostAsJsonAsync("/api/growth/loyalty/points",new LoyaltyChangeRequest(customerId,2500,"Completed journeys",null));Assert.Equal(HttpStatusCode.OK,points.StatusCode);var loyalty=await points.Content.ReadFromJsonAsync<LoyaltySummaryDto>();Assert.Equal(LoyaltyTier.Silver,loyalty!.Tier);Assert.Equal(2500,loyalty.PointsBalance);Assert.Single(loyalty.History);
        await using var auditScope=host.App.Services.CreateAsyncScope();var auditDb=auditScope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();Assert.True(await auditDb.SecurityAuditEvents.AnyAsync(x=>x.CompanyId==LondonVipCompany.Id&&x.EventType=="ReferralQualified"));Assert.True(await auditDb.SecurityAuditEvents.AnyAsync(x=>x.CompanyId==LondonVipCompany.Id&&x.EventType=="LoyaltyPointsChanged"));
    }

    [Fact]
    public async Task PublicLeadAndNewsletterFeedTenantGrowthWhileAdministrationRequiresAuthorization()
    {
        await using var host=await TestApiHost.StartAsync();
        using var subscribe=new HttpRequestMessage(HttpMethod.Post,"/api/growth/newsletters/subscribe"){Content=JsonContent.Create(new NewsletterRequest("reader@example.test","Reader","Airport","Leisure"))};subscribe.Headers.Add("X-Test-Anonymous","true");Assert.Equal(HttpStatusCode.Accepted,(await host.Client.SendAsync(subscribe)).StatusCode);
        using var lead=new HttpRequestMessage(HttpMethod.Post,"/api/growth/leads"){Content=JsonContent.Create(new LeadCaptureRequest("CorporateEnquiry","Website","Autumn","Ada Lovelace","ada@example.test",null,"Call me"))};lead.Headers.Add("X-Test-Anonymous","true");Assert.Equal(HttpStatusCode.Accepted,(await host.Client.SendAsync(lead)).StatusCode);
        using var anonymous=new HttpRequestMessage(HttpMethod.Get,"/api/growth/dashboard");anonymous.Headers.Add("X-Test-Anonymous","true");Assert.Equal(HttpStatusCode.Unauthorized,(await host.Client.SendAsync(anonymous)).StatusCode);
        using var finance=new HttpRequestMessage(HttpMethod.Post,"/api/growth/campaigns"){Content=JsonContent.Create(new CampaignRequest("Campaign",MarketingChannel.Email,"all","template",null))};finance.Headers.Add("X-Test-Role",SecurityRoles.Finance);Assert.Equal(HttpStatusCode.Forbidden,(await host.Client.SendAsync(finance)).StatusCode);
        await using var scope=host.App.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();Assert.True(await db.NewsletterSubscribers.AnyAsync(x=>x.CompanyId==LondonVipCompany.Id&&x.Email=="READER@EXAMPLE.TEST"));Assert.True(await db.LeadCaptures.AnyAsync(x=>x.CompanyId==LondonVipCompany.Id&&x.Email=="ada@example.test"));Assert.True(await db.CrmLeads.AnyAsync(x=>x.CompanyId==LondonVipCompany.Id&&x.Email=="ada@example.test"));
    }

    private static Company Company(Guid id)=>new(){Id=id,TradingName="Other",LegalName="Other Ltd",Slug=$"other-{id:N}",Email="office@other.test",Phone="07000000000",WebsiteUrl="",AddressLine1="1 Road",AddressLine2="",City="London",Postcode="SW1A 1AA",Country="GB",TimeZone="Europe/London",CurrencyCode="GBP",IsActive=true,CreatedAt=DateTimeOffset.UtcNow,UpdatedAt=DateTimeOffset.UtcNow};
}
