using System.Net;
using System.Net.Http.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Pricing;
using LondonVIP.Shared.Quotations;
using LondonVIP.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LondonVIP.Tests;

public class QuotationEndpointTests
{
    [Fact]
    public async Task CreateQuote_UsesV2PricingAndPersistsCorporatePromotionSnapshot()
    {
        await using var host=await TestApiHost.StartAsync();var seed=await SeedAsync(host);
        using var response=await host.Client.PostAsJsonAsync("/api/quotations",Request(seed,true,"SAVE10"));
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);var quote=await response.Content.ReadFromJsonAsync<QuotationDetailDto>();
        Assert.NotNull(quote);Assert.StartsWith("Q-",quote.QuoteReference);Assert.Equal(80m,quote.TotalFare);Assert.Equal(20m,quote.DiscountTotal);Assert.Equal(seed.AccountId,quote.CorporateAccountId);Assert.NotEmpty(quote.PricingBreakdown);
    }

    [Fact]
    public async Task ConvertQuote_PreservesPriceAndRepeatedConversionReturnsExistingBooking()
    {
        await using var host=await TestApiHost.StartAsync();var seed=await SeedAsync(host);var quote=await CreateAsync(host,Request(seed,true,"SAVE10"));
        using var first=await host.Client.PostAsync($"/api/quotations/{quote.Id}/convert",null);Assert.Equal(HttpStatusCode.Created,first.StatusCode);var created=await first.Content.ReadFromJsonAsync<QuotationConversionDto>();
        using var second=await host.Client.PostAsync($"/api/quotations/{quote.Id}/convert",null);Assert.Equal(HttpStatusCode.OK,second.StatusCode);var repeated=await second.Content.ReadFromJsonAsync<QuotationConversionDto>();
        Assert.NotNull(created);Assert.Equal(created.BookingId,repeated?.BookingId);Assert.True(repeated?.AlreadyConverted);
        await using var scope=host.App.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();var booking=db.Bookings.Single(x=>x.Id==created.BookingId);Assert.Equal(quote.BaseFare,booking.BaseFare);Assert.Equal(quote.Extras,booking.Extras);Assert.Equal(quote.TotalFare,booking.TotalFare);Assert.Single(db.Bookings.Where(x=>x.Id==created.BookingId));Assert.Contains(db.SecurityAuditEvents,x=>x.EventType=="QuotationConverted"&&x.ResourceIdentifier==quote.Id.ToString());
    }

    [Fact]
    public async Task ExpiredQuote_CannotBeConverted()
    {
        await using var host=await TestApiHost.StartAsync();var seed=await SeedAsync(host);var quote=await CreateAsync(host,Request(seed,false,null));
        await using(var scope=host.App.Services.CreateAsyncScope()){var db=scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();var entity=db.Quotations.Single(x=>x.Id==quote.Id);entity.ExpiresAt=DateTimeOffset.UtcNow.AddMinutes(-1);await db.SaveChangesAsync();}
        using var response=await host.Client.PostAsync($"/api/quotations/{quote.Id}/convert",null);Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
    }

    [Fact]
    public async Task CustomerPortal_AcceptsThenConvertsOwnQuote()
    {
        await using var host=await TestApiHost.StartAsync();var seed=await SeedAsync(host);var quote=await CreateAsync(host,Request(seed,false,null));
        using var accept=await host.Client.PostAsync($"/api/customer-portal/{seed.CustomerId}/quotes/{quote.Id}/accept",null);Assert.Equal(HttpStatusCode.OK,accept.StatusCode);
        using var convert=await host.Client.PostAsync($"/api/customer-portal/{seed.CustomerId}/quotes/{quote.Id}/convert",null);Assert.Equal(HttpStatusCode.Created,convert.StatusCode);
    }

    [Fact]
    public async Task CrossTenantAndUnauthorizedQuotationAccessAreBlocked()
    {
        await using var host=await TestApiHost.StartAsync();var other=await AddOtherTenantQuoteAsync(host);
        using var hidden=await host.Client.GetAsync($"/api/quotations/{other}");Assert.Equal(HttpStatusCode.NotFound,hidden.StatusCode);
        using var request=new HttpRequestMessage(HttpMethod.Get,"/api/quotations");request.Headers.Add("X-Test-Anonymous","true");using var unauthorized=await host.Client.SendAsync(request);Assert.Equal(HttpStatusCode.Unauthorized,unauthorized.StatusCode);
    }

    private static async Task<Seed> SeedAsync(TestApiHost host)
    {var now=DateTimeOffset.UtcNow;var customer=new Customer{Id=Guid.NewGuid(),CompanyId=LondonVipCompany.Id,FirstName="Quote",LastName="Customer",Email=$"{Guid.NewGuid():N}@test.local",Phone="02070000000",IsActive=true,CreatedAt=now,UpdatedAt=now};var account=new CorporateAccount{Id=Guid.NewGuid(),CompanyId=LondonVipCompany.Id,AccountNumber=$"A-{Guid.NewGuid():N}"[..12],AccountName="Quote Corp",PrimaryContactName="Tester",Email="corp@test.local",Phone="02070000001",AddressLine1="1 Test",TownCity="London",Postcode="W1",Country="UK",IsActive=true,CreatedAt=now,UpdatedAt=now};
        var rules=new[]{Rule(PricingRuleType.ZoneFixedFare,100),Rule(PricingRuleType.CorporateDiscount,0,10),Rule(PricingRuleType.PromotionalDiscount,0,10,"SAVE10")};await using var scope=host.App.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();db.AddRange(customer,account);db.PricingRules.AddRange(rules);await db.SaveChangesAsync();return new(customer.Id,account.Id);}
    private static PricingRule Rule(PricingRuleType type,decimal amount,decimal percentage=0,string? code=null)=>new(){Id=Guid.NewGuid(),CompanyId=LondonVipCompany.Id,RuleType=type,Name=type.ToString(),VehicleType=VehicleType.MPV,PickupZone="West",DestinationZone="Central",PromotionCode=code,Amount=amount,Percentage=percentage,IsActive=true,CreatedAt=DateTimeOffset.UtcNow,UpdatedAt=DateTimeOffset.UtcNow};
    private static QuotationCreateDto Request(Seed seed,bool corporate,string? promotion)=>new(){CustomerId=seed.CustomerId,CorporateAccountId=corporate?seed.AccountId:null,PickupDateTime=DateTimeOffset.UtcNow.AddDays(2),ExpiresAt=DateTimeOffset.UtcNow.AddDays(1),Pricing=new(){PickupAddress="Hammersmith",Destination="Mayfair",PickupZone="West",DestinationZone="Central",PassengerCount=2,LuggageCount=1,VehicleType=VehicleType.MPV,IsCorporateCustomer=corporate,PromotionCode=promotion,JourneyDateTime=DateTimeOffset.UtcNow.AddDays(2)}};
    private static async Task<QuotationDetailDto> CreateAsync(TestApiHost host,QuotationCreateDto request){using var response=await host.Client.PostAsJsonAsync("/api/quotations",request);response.EnsureSuccessStatusCode();return(await response.Content.ReadFromJsonAsync<QuotationDetailDto>())!;}
    private static async Task<Guid> AddOtherTenantQuoteAsync(TestApiHost host){var now=DateTimeOffset.UtcNow;var companyId=Guid.NewGuid();var customerId=Guid.NewGuid();var quoteId=Guid.NewGuid();var company=new Company{Id=companyId,TradingName="Other",LegalName="Other",Slug=$"other-{companyId:N}",City="London",Country="UK",TimeZone="Europe/London",CurrencyCode="GBP",IsActive=true,CreatedAt=now,UpdatedAt=now};var customer=new Customer{Id=customerId,CompanyId=companyId,FirstName="Other",LastName="Customer",Email="other@test.local",Phone="1",IsActive=true,CreatedAt=now,UpdatedAt=now};var quote=new Quotation{Id=quoteId,CompanyId=companyId,CustomerId=customerId,QuoteReference="Q-OTHER",Status=QuoteStatus.Active,ExpiresAt=now.AddDays(1),PickupAddress="A",Destination="B",PickupDateTime=now.AddDays(2),PassengerCount=1,VehicleType=VehicleType.Saloon,PricingBreakdownJson="[]",PricingRequestJson="{}",CreatedAt=now,UpdatedAt=now};await using var scope=host.App.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();db.AddRange(company,customer,quote);await db.SaveChangesAsync();return quoteId;}
    private sealed record Seed(Guid CustomerId,Guid AccountId);
}
