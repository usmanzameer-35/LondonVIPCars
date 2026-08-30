using System.Net;
using System.Net.Http.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.CustomerPortal;
using LondonVIP.Shared.Models;
using LondonVIP.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LondonVIP.Tests;

public class CustomerPortalEndpointTests
{
    [Fact]
    public async Task Dashboard_ReturnsOnlySelectedCustomersTenantScopedRecords()
    {
        await using var host = await TestApiHost.StartAsync();
        var data = await SeedPortalAsync(host);

        var dashboard = await host.Client.GetFromJsonAsync<CustomerPortalDashboardDto>($"/api/customer-portal/{data.CustomerId}");

        Assert.NotNull(dashboard);
        Assert.Equal(data.CustomerId, dashboard.CustomerId);
        var booking = Assert.Single(dashboard.Bookings);
        Assert.Equal(data.BookingId, booking.Id);
        Assert.Equal(data.InvoiceNumber, booking.InvoiceNumber);
        var invoice = Assert.Single(dashboard.Invoices);
        Assert.Equal(data.InvoiceNumber, invoice.InvoiceNumber);
        var payment = Assert.Single(dashboard.Payments);
        Assert.Equal(120m, payment.Amount);
        Assert.Equal(120m, payment.AllocatedAmount);
    }

    [Fact]
    public async Task BookingDetail_ReturnsStatusAndOperationalDetailForOwningCustomer()
    {
        await using var host = await TestApiHost.StartAsync();
        var data = await SeedPortalAsync(host);

        var detail = await host.Client.GetFromJsonAsync<CustomerPortalBookingDetailDto>($"/api/customer-portal/{data.CustomerId}/bookings/{data.BookingId}");

        Assert.NotNull(detail);
        Assert.Equal(BookingStatus.Confirmed.ToString(), detail.Booking.Status);
        Assert.Equal("BA123", detail.Booking.FlightNumber);
        Assert.True(detail.IsAirportPickup);
        Assert.Equal(data.InvoiceNumber, detail.Booking.InvoiceNumber);
    }

    [Fact]
    public async Task BookingDetail_ReturnsNotFoundForAnotherCustomerAndAuditsAttempt()
    {
        await using var host = await TestApiHost.StartAsync();
        var data = await SeedPortalAsync(host);
        var otherCustomerId = await AddCurrentTenantCustomerAsync(host);

        using var response = await host.Client.GetAsync($"/api/customer-portal/{otherCustomerId}/bookings/{data.BookingId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using var scope = host.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        Assert.Contains(db.SecurityAuditEvents, item => item.EventType == "CustomerPortalAccessDenied" && item.ResourceIdentifier == data.BookingId.ToString());
    }

    [Fact]
    public async Task CrossTenantCustomer_ReturnsNotFoundAndIsAudited()
    {
        await using var host = await TestApiHost.StartAsync();
        var customerId = await AddOtherTenantCustomerAsync(host);

        using var response = await host.Client.GetAsync($"/api/customer-portal/{customerId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using var scope = host.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        Assert.Contains(db.SecurityAuditEvents, item => item.EventType == "CrossTenantAccessAttempt" && item.ResourceIdentifier == customerId.ToString());
    }

    [Fact]
    public async Task PortalEndpoints_RequireAuthentication()
    {
        await using var host = await TestApiHost.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/customer-portal/customers");
        request.Headers.Add("X-Test-Anonymous", "true");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<PortalSeed> SeedPortalAsync(TestApiHost host)
    {
        var now = DateTimeOffset.UtcNow;
        var customer = NewCustomer(LondonVipCompany.Id, "portal@example.test");
        var booking = new Booking
        {
            Id = Guid.NewGuid(), BookingReference = "LVC-PORTAL-001", CompanyId = LondonVipCompany.Id, CustomerId = customer.Id,
            PickupAddress = "Heathrow Terminal 5", Destination = "Mayfair", PickupDateTime = now.AddHours(2), PassengerCount = 2,
            LuggageCount = 2, VehicleType = VehicleType.Saloon, IsAirportPickup = true, IsMeetAndGreet = true, FlightNumber = "BA123",
            BaseFare = 120m, TotalFare = 120m, Status = BookingStatus.Confirmed, PaymentStatus = "Paid", CreatedAt = now, UpdatedAt = now
        };
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, CustomerId = customer.Id, InvoiceNumber = "LVC-000999",
            InvoiceDate = now, DueDate = now.AddDays(14), Status = InvoiceStatus.Paid, Subtotal = 120m, TotalAmount = 120m,
            AmountPaid = 120m, BalanceDue = 0m, CreatedAt = now, UpdatedAt = now
        };
        var line = new InvoiceLine { Id = Guid.NewGuid(), InvoiceId = invoice.Id, BookingId = booking.Id, Description = "Airport transfer", Quantity = 1, UnitPrice = 120m, LineSubtotal = 120m, LineTotal = 120m, CreatedAt = now };
        var payment = new Payment { Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, CustomerId = customer.Id, PaymentReference = "PAY-PORTAL-001", PaymentDate = now, PaymentMethod = PaymentMethod.Card, Amount = 120m, CreatedAt = now, UpdatedAt = now };
        var allocation = new PaymentAllocation { Id = Guid.NewGuid(), PaymentId = payment.Id, InvoiceId = invoice.Id, Amount = 120m, CreatedAt = now };
        await using var scope = host.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        db.AddRange(customer, booking, invoice, line, payment, allocation);
        await db.SaveChangesAsync();
        return new PortalSeed(customer.Id, booking.Id, invoice.InvoiceNumber);
    }

    private static async Task<Guid> AddCurrentTenantCustomerAsync(TestApiHost host)
    {
        var customer = NewCustomer(LondonVipCompany.Id, "second@example.test");
        await using var scope = host.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        db.Customers.Add(customer); await db.SaveChangesAsync(); return customer.Id;
    }

    private static async Task<Guid> AddOtherTenantCustomerAsync(TestApiHost host)
    {
        var id = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var company = new Company { Id = id, TradingName = "Other Cars", LegalName = "Other Cars", Slug = $"other-{id:N}", City = "London", Country = "UK", TimeZone = "Europe/London", CurrencyCode = "GBP", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var customer = NewCustomer(id, "other-portal@example.test");
        await using var scope = host.App.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        db.AddRange(company, customer); await db.SaveChangesAsync(); return customer.Id;
    }

    private static Customer NewCustomer(Guid companyId, string email)
    {
        var now = DateTimeOffset.UtcNow;
        return new Customer { Id = Guid.NewGuid(), CompanyId = companyId, FirstName = "Portal", LastName = "Customer", Email = email, Phone = "020 7000 0000", CreatedAt = now, UpdatedAt = now, IsActive = true };
    }

    private sealed record PortalSeed(Guid CustomerId, Guid BookingId, string InvoiceNumber);
}
