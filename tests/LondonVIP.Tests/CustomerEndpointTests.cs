using System.Net;
using System.Net.Http.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.Customers;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Security;
using LondonVIP.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LondonVIP.Tests;

public class CustomerEndpointTests
{
    [Fact]
    public async Task CreateCustomer_IgnoresCompanyIdAndCreatesAuditEvent()
    {
        await using var host = await TestApiHost.StartAsync();
        var payload = new { companyId = Guid.NewGuid(), firstName = "Ada", lastName = "Lovelace", email = "ADA@example.test", phone = "020 7000 0000", postcode = "sw1a 1aa", notes = "Prefers quiet journeys", isActive = true };
        using var response = await host.Client.PostAsJsonAsync("/api/customers", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var customer = await response.Content.ReadFromJsonAsync<CustomerDetailDto>();
        Assert.NotNull(customer);
        Assert.Equal("ada@example.test", customer.Email);
        Assert.Equal("SW1A 1AA", customer.Postcode);

        await using var scope = host.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        Assert.Equal(LondonVipCompany.Id, db.Customers.Single(item => item.Id == customer.Id).CompanyId);
        Assert.Contains(db.SecurityAuditEvents, item => item.EventType == "CustomerCreated" && item.ResourceIdentifier == customer.Id.ToString());
    }

    [Fact]
    public async Task UpdateCustomer_ChangesOperationalFieldsAndCreatesAuditEvent()
    {
        await WithCustomerAsync(async (host, customer) =>
        {
            var update = Valid("updated@example.test"); update.FirstName = "Updated"; update.SecondaryPhone = "+44 7700 900123"; update.Address = "1 Test Street"; update.Notes = "Updated note"; update.IsActive = false;
            using var response = await host.Client.PutAsJsonAsync($"/api/customers/{customer.Id}", update);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var changed = await response.Content.ReadFromJsonAsync<CustomerDetailDto>();
            Assert.Equal("Updated", changed?.FirstName); Assert.False(changed?.IsActive); Assert.Equal("Updated note", changed?.Notes);
            await using var scope = host.App.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
            Assert.Contains(db.SecurityAuditEvents, item => item.EventType == "CustomerUpdated" && item.ResourceIdentifier == customer.Id.ToString());
        });
    }

    [Fact]
    public async Task ListAndDetail_ReturnOnlyCurrentTenantWithActivitySummary()
    {
        await WithCustomerAsync(async (host, customer) =>
        {
            await AddBookingsAsync(host, customer.Id);
            var other = await AddOtherTenantCustomerAsync(host);
            var list = await host.Client.GetFromJsonAsync<List<CustomerListItemDto>>("/api/customers");
            var current = Assert.Single(list!, item => item.Id == customer.Id);
            Assert.DoesNotContain(list!, item => item.Id == other.Id);
            Assert.Equal(2, current.TotalBookings); Assert.Equal(75m, current.TotalSpend); Assert.NotNull(current.LastBookingDate);

            var detail = await host.Client.GetFromJsonAsync<CustomerDetailDto>($"/api/customers/{customer.Id}");
            Assert.NotNull(detail); Assert.Equal(2, detail.Activity.TotalBookings); Assert.Equal(1, detail.Activity.CompletedBookings); Assert.Equal(1, detail.Activity.UpcomingBookings); Assert.Equal(75m, detail.Activity.TotalSpend);
        });
    }

    [Fact]
    public async Task BookingHistory_ReturnsRealCustomerJourneysOrderedNewestFirst()
    {
        await WithCustomerAsync(async (host, customer) =>
        {
            await AddBookingsAsync(host, customer.Id);
            var history = await host.Client.GetFromJsonAsync<List<CustomerBookingHistoryItemDto>>($"/api/customers/{customer.Id}/bookings");
            Assert.Equal(2, history?.Count); Assert.True(history![0].PickupDateTime > history[1].PickupDateTime);
            Assert.Contains(history, item => item.Status == BookingStatus.Completed && item.TotalFare == 75m);
        });
    }

    [Fact]
    public async Task DuplicateEmail_IsCaseInsensitiveWithinTenant()
    {
        await WithCustomerAsync(async (host, customer) =>
        {
            using var response = await host.Client.PostAsJsonAsync("/api/customers", Valid(customer.Email.ToUpperInvariant()));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<ValidationResponse>();
            Assert.Contains("email", problem!.Errors.Keys, StringComparer.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task PhoneOnlyCustomer_IsAllowed_ButInvalidDataIsRejected()
    {
        await using var host = await TestApiHost.StartAsync();
        var phoneOnly = Valid(string.Empty); phoneOnly.Phone = "+44 20 7000 0000";
        using var validResponse = await host.Client.PostAsJsonAsync("/api/customers", phoneOnly);
        Assert.Equal(HttpStatusCode.Created, validResponse.StatusCode);

        var invalid = new CustomerCreateDto { FirstName = "", LastName = "", Email = "not-an-email", Phone = "abc", Notes = new string('x', 4001) };
        using var invalidResponse = await host.Client.PostAsJsonAsync("/api/customers", invalid);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        var problem = await invalidResponse.Content.ReadFromJsonAsync<ValidationResponse>();
        Assert.Contains("firstName", problem!.Errors.Keys, StringComparer.OrdinalIgnoreCase); Assert.Contains("email", problem.Errors.Keys, StringComparer.OrdinalIgnoreCase); Assert.Contains("phone", problem.Errors.Keys, StringComparer.OrdinalIgnoreCase); Assert.Contains("notes", problem.Errors.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CrossTenantCustomerAccess_ReturnsNotFoundAndIsAudited()
    {
        await using var host = await TestApiHost.StartAsync();
        var other = await AddOtherTenantCustomerAsync(host);
        using var detail = await host.Client.GetAsync($"/api/customers/{other.Id}");
        using var history = await host.Client.GetAsync($"/api/customers/{other.Id}/bookings");
        using var update = await host.Client.PutAsJsonAsync($"/api/customers/{other.Id}", Valid("blocked@example.test"));
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode); Assert.Equal(HttpStatusCode.NotFound, history.StatusCode); Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        await using var scope = host.App.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        Assert.Contains(db.SecurityAuditEvents, item => item.EventType == "CrossTenantAccessAttempt" && item.ResourceIdentifier == other.Id.ToString());
    }

    [Fact]
    public async Task FinanceHasReadOnlyAccess_AndDispatcherCanWrite()
    {
        await using var host = await TestApiHost.StartAsync();
        using var financeRead = Request<object>(HttpMethod.Get, "/api/customers", SecurityRoles.Finance);
        using var financeReadResponse = await host.Client.SendAsync(financeRead);
        Assert.Equal(HttpStatusCode.OK, financeReadResponse.StatusCode);
        using var financeWrite = Request(HttpMethod.Post, "/api/customers", SecurityRoles.Finance, Valid("finance@example.test"));
        using var financeWriteResponse = await host.Client.SendAsync(financeWrite);
        Assert.Equal(HttpStatusCode.Forbidden, financeWriteResponse.StatusCode);
        using var dispatcherWrite = Request(HttpMethod.Post, "/api/customers", SecurityRoles.Dispatcher, Valid("dispatcher@example.test"));
        using var dispatcherWriteResponse = await host.Client.SendAsync(dispatcherWrite);
        Assert.Equal(HttpStatusCode.Created, dispatcherWriteResponse.StatusCode);
    }

    private static async Task WithCustomerAsync(Func<TestApiHost, Customer, Task> test)
    {
        await using var host = await TestApiHost.StartAsync();
        var customer = NewCustomer(LondonVipCompany.Id, "current@example.test");
        await using (var scope = host.App.Services.CreateAsyncScope()) { var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>(); db.Customers.Add(customer); await db.SaveChangesAsync(); }
        await test(host, customer);
    }

    private static async Task AddBookingsAsync(TestApiHost host, Guid customerId)
    {
        var now = DateTimeOffset.UtcNow;
        var completed = NewBooking(customerId, BookingStatus.Completed, now.AddDays(-2), 75m);
        var upcoming = NewBooking(customerId, BookingStatus.Confirmed, now.AddDays(2), 50m);
        await using var scope = host.App.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>(); db.Bookings.AddRange(completed, upcoming); await db.SaveChangesAsync();
    }

    private static async Task<Customer> AddOtherTenantCustomerAsync(TestApiHost host)
    {
        var id = Guid.NewGuid(); var company = new Company { Id = id, TradingName = "Other Cars", LegalName = "Other Cars", Slug = $"other-{id:N}", Email = "", Phone = "", WebsiteUrl = "", AddressLine1 = "", AddressLine2 = "", City = "London", Postcode = "", Country = "UK", TimeZone = "Europe/London", CurrencyCode = "GBP", IsActive = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var customer = NewCustomer(id, "other@example.test");
        await using var scope = host.App.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>(); db.AddRange(company, customer); await db.SaveChangesAsync(); return customer;
    }

    private static Customer NewCustomer(Guid companyId, string email) { var now = DateTimeOffset.UtcNow; return new Customer { Id = Guid.NewGuid(), CompanyId = companyId, FirstName = "Test", LastName = "Customer", Email = email, Phone = "020 7000 0000", Postcode = "W6 0AA", CreatedAt = now, UpdatedAt = now, IsActive = true }; }
    private static Booking NewBooking(Guid customerId, BookingStatus status, DateTimeOffset pickup, decimal fare) { var id = Guid.NewGuid(); return new Booking { Id = id, BookingReference = $"LVC-{id:N}"[..20], CompanyId = LondonVipCompany.Id, CustomerId = customerId, PickupAddress = "Heathrow", Destination = "Mayfair", PickupDateTime = pickup, PassengerCount = 1, LuggageCount = 1, VehicleType = VehicleType.Saloon, TotalFare = fare, BaseFare = fare, Status = status, PaymentStatus = "Paid", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }; }
    private static CustomerUpdateDto Valid(string email) => new() { FirstName = "Grace", LastName = "Hopper", Email = email, Phone = "020 7000 0000", IsActive = true };
    private static HttpRequestMessage Request<T>(HttpMethod method, string uri, string role, T? body = default) { var request = new HttpRequestMessage(method, uri); request.Headers.Add("X-Test-Role", role); if (body is not null) request.Content = JsonContent.Create(body); return request; }
    private sealed class ValidationResponse { public Dictionary<string, string[]> Errors { get; set; } = []; }
}
