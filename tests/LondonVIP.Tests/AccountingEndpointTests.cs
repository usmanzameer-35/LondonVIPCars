using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.Accounting;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Security;
using LondonVIP.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LondonVIP.Tests;

public sealed class AccountingEndpointTests
{
    [Fact]
    public async Task BalancedJournalCanPostAndProducesTrialBalanceAndAudit()
    {
        await using var host = await TestApiHost.StartAsync();
        Guid cash, revenue;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
            cash = Guid.NewGuid(); revenue = Guid.NewGuid();
            db.LedgerAccounts.AddRange(Account(cash, "1000", LedgerAccountType.Asset), Account(revenue, "4000", LedgerAccountType.Revenue));
            await db.SaveChangesAsync();
        }
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = new JournalRequest("JRN-001", today, "Sale", "Manual", null,
            [new(cash, "Cash", 100, 0, null, null), new(revenue, "Revenue", 0, 100, null, null)]);
        using var created = await host.Client.PostAsJsonAsync("/api/finance/journals", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var journal = await created.Content.ReadFromJsonAsync<JournalResult>(); Assert.NotNull(journal);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsync($"/api/finance/journals/{journal.Id}/post", null)).StatusCode);
        var trial = await host.Client.GetFromJsonAsync<TrialBalanceDto>($"/api/finance/trial-balance?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
        Assert.NotNull(trial); Assert.Equal(100m, trial.TotalDebits); Assert.Equal(trial.TotalDebits, trial.TotalCredits);
        await using var audit = host.App.Services.CreateAsyncScope();
        Assert.True(await audit.ServiceProvider.GetRequiredService<LondonVIPDbContext>().SecurityAuditEvents.AnyAsync(x => x.EventType == "JournalPosted" && x.CompanyId == LondonVipCompany.Id));
    }

    [Fact]
    public async Task UnbalancedJournalIsRejectedAndFinanceDataIsTenantScoped()
    {
        await using var host = await TestApiHost.StartAsync(); Guid account;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>(); account = Guid.NewGuid();
            db.LedgerAccounts.Add(Account(account, "1001", LedgerAccountType.Asset));
            var other = Guid.NewGuid(); db.Companies.Add(Company(other));
            db.LedgerAccounts.Add(new() { Id=Guid.NewGuid(), CompanyId=other, Code="SECRET", Name="Other tenant", Type=LedgerAccountType.Asset, CreatedAt=DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        using var invalid = await host.Client.PostAsJsonAsync("/api/finance/journals", new JournalRequest("BAD", DateOnly.FromDateTime(DateTime.UtcNow), "Bad", "Manual", null, [new(account, "Bad", 10, 0, null, null)]));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var rows = await host.Client.GetFromJsonAsync<List<JsonElement>>("/api/finance/accounts"); var row = Assert.Single(rows!); Assert.Equal("1001", row.GetProperty("code").GetString());
        using var anonymous = new HttpRequestMessage(HttpMethod.Get, "/api/finance"); anonymous.Headers.Add("X-Test-Anonymous", "true"); Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.SendAsync(anonymous)).StatusCode);
        using var dispatcher = new HttpRequestMessage(HttpMethod.Get, "/api/finance"); dispatcher.Headers.Add("X-Test-Role", SecurityRoles.Dispatcher); Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.SendAsync(dispatcher)).StatusCode);
    }

    [Fact]
    public async Task ExpensesFeedVatAndDriverSettlementsUseCompletedTenantBookings()
    {
        await using var host = await TestApiHost.StartAsync(); Guid driver; var date = DateOnly.FromDateTime(DateTime.UtcNow);
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>(); driver = Guid.NewGuid(); var customer = Guid.NewGuid();
            db.Customers.Add(new() { Id=customer, CompanyId=LondonVipCompany.Id, FirstName="Alan", LastName="Turing", Email="alan@example.test", Phone="07700900003", IsActive=true, CreatedAt=DateTimeOffset.UtcNow, UpdatedAt=DateTimeOffset.UtcNow });
            db.Drivers.Add(new() { Id=driver, CompanyId=LondonVipCompany.Id, FirstName="Mary", LastName="Jackson", Phone="07700900002", Email="mary@example.test", IsActive=true });
            db.Bookings.Add(new() { Id=Guid.NewGuid(), CompanyId=LondonVipCompany.Id, BookingReference="LV-ACC-1", CustomerId=customer, PickupAddress="A", Destination="B", PickupDateTime=DateTimeOffset.UtcNow, PassengerCount=1, VehicleType=VehicleType.Saloon, TotalFare=200, DriverId=driver, Status=BookingStatus.Completed, PaymentStatus="Paid", CreatedAt=DateTimeOffset.UtcNow, UpdatedAt=DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        using var expense = await host.Client.PostAsJsonAsync("/api/finance/expenses", new ExpenseRequest("EXP-1", "Fuel", "Fuel", date, 50, 10, driver, null, "receipts/1.pdf", "Fleet", "Vehicles")); Assert.Equal(HttpStatusCode.Created, expense.StatusCode);
        var vat = await host.Client.GetFromJsonAsync<VatReportDto>($"/api/finance/vat?from={date:yyyy-MM-dd}&to={date:yyyy-MM-dd}"); Assert.NotNull(vat); Assert.Equal(10m, vat.InputVat);
        using var settlement = await host.Client.PostAsJsonAsync("/api/finance/driver-settlements", new DriverSettlementRequest(driver, date, date, 10, 5, 0)); Assert.Equal(HttpStatusCode.Created, settlement.StatusCode);
        await using var resultScope = host.App.Services.CreateAsyncScope(); var result = Assert.Single(await resultScope.ServiceProvider.GetRequiredService<LondonVIPDbContext>().DriverSettlements.ToListAsync());
        Assert.Equal(200m, result.GrossFares); Assert.Equal(205m-result.Commission, result.NetPayable);
    }

    private static LedgerAccount Account(Guid id,string code,LedgerAccountType type)=>new(){Id=id,CompanyId=LondonVipCompany.Id,Code=code,Name=code,Type=type,AllowPosting=true,IsActive=true,CreatedAt=DateTimeOffset.UtcNow};
    private static Company Company(Guid id)=>new(){Id=id,TradingName="Other",LegalName="Other Ltd",Slug=$"other-{id:N}",Email="office@other.test",Phone="07000000000",WebsiteUrl="",AddressLine1="1 Road",AddressLine2="",City="London",Postcode="SW1A 1AA",Country="GB",TimeZone="Europe/London",CurrencyCode="GBP",IsActive=true,CreatedAt=DateTimeOffset.UtcNow,UpdatedAt=DateTimeOffset.UtcNow};
}
