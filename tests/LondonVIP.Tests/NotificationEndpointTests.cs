using System.Net;
using System.Net.Http.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.Bookings;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Notifications;
using LondonVIP.Shared.Pricing;
using LondonVIP.Shared.Quotations;
using LondonVIP.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LondonVIP.Tests;

public sealed class NotificationEndpointTests
{
    [Fact]
    public async Task BookingCreation_QueuesTenantScopedNotification()
    {
        await using var host = await TestApiHost.StartAsync();
        var customer = await AddCustomerAsync(host);
        var request = new BookingCreateDto { CustomerId = customer.Id, PickupAddress = "Heathrow", Destination = "Mayfair", PickupDateTime = DateTimeOffset.UtcNow.AddDays(1), PassengerCount = 1, VehicleType = VehicleType.Saloon, BaseFare = 50, TotalFare = 50, PaymentStatus = "Pending" };

        using var response = await host.Client.PostAsJsonAsync("/api/bookings", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var scope = host.App.Services.CreateAsyncScope();
        var notification = await scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>().Notifications.SingleAsync();
        Assert.Equal(LondonVipCompany.Id, notification.CompanyId);
        Assert.Equal(NotificationType.BookingCreated, notification.NotificationType);
        Assert.Equal(customer.Email, notification.Recipient);
    }

    [Fact]
    public async Task QuoteAndInvoiceOperations_QueueNotifications()
    {
        await using var host = await TestApiHost.StartAsync();
        var customer = await AddCustomerAsync(host);
        await AddPricingRuleAsync(host);
        var quoteRequest = new QuotationCreateDto { CustomerId = customer.Id, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1), PickupDateTime = DateTimeOffset.UtcNow.AddDays(2), Pricing = new QuoteRequest { PickupAddress = "West", Destination = "Central", PickupZone = "West", DestinationZone = "Central", JourneyDateTime = DateTimeOffset.UtcNow.AddDays(2), PassengerCount = 1, VehicleType = VehicleType.Saloon } };
        using var quoteResponse = await host.Client.PostAsJsonAsync("/api/quotations", quoteRequest);
        Assert.Equal(HttpStatusCode.Created, quoteResponse.StatusCode);

        var booking = await AddCompletedBookingAsync(host, customer.Id);
        using var invoiceResponse = await host.Client.PostAsync($"/api/bookings/{booking.Id}/invoice", null);
        Assert.Equal(HttpStatusCode.Created, invoiceResponse.StatusCode);

        await using var scope = host.App.Services.CreateAsyncScope();
        var types = await scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>().Notifications.Select(x => x.NotificationType).ToListAsync();
        Assert.Contains(NotificationType.QuoteCreated, types);
        Assert.Contains(NotificationType.InvoiceGenerated, types);
    }

    [Fact]
    public async Task Retry_SendsNotificationAndCreatesAuditEvent()
    {
        await using var host = await TestApiHost.StartAsync();
        var notification = await AddNotificationAsync(host, LondonVipCompany.Id);
        using var response = await host.Client.PostAsync($"/api/notifications/{notification.Id}/retry", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = host.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        var updated = await db.Notifications.SingleAsync(x => x.Id == notification.Id);
        Assert.Equal(NotificationStatus.Sent, updated.Status);
        Assert.Equal(1, updated.RetryCount);
        Assert.Contains(db.SecurityAuditEvents, x => x.EventType == "NotificationResent" && x.ResourceIdentifier == notification.Id.ToString());
    }

    [Fact]
    public async Task NotificationEndpoints_EnforceTenantIsolationAndAuthorization()
    {
        await using var host = await TestApiHost.StartAsync();
        var otherCompany = Guid.NewGuid();
        var notification = await AddNotificationAsync(host, otherCompany);
        using var hidden = await host.Client.GetAsync($"/api/notifications/{notification.Id}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using var anonymousRequest = new HttpRequestMessage(HttpMethod.Get, "/api/notifications");
        anonymousRequest.Headers.Add("X-Test-Anonymous", "true");
        using var anonymous = await host.Client.SendAsync(anonymousRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    private static async Task<Customer> AddCustomerAsync(TestApiHost host)
    {
        var customer = new Customer { Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, FirstName = "Notify", LastName = "Customer", Email = $"{Guid.NewGuid():N}@test.local", Phone = "02070000000", IsActive = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        await using var scope = host.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        db.Customers.Add(customer); await db.SaveChangesAsync(); return customer;
    }

    private static async Task AddPricingRuleAsync(TestApiHost host)
    {
        await using var scope = host.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        db.PricingRules.Add(new PricingRule { Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, Name = "Notification test fare", RuleType = PricingRuleType.ZoneFixedFare, VehicleType = VehicleType.Saloon, PickupZone = "West", DestinationZone = "Central", Amount = 50, IsActive = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
    }

    private static async Task<Booking> AddCompletedBookingAsync(TestApiHost host, Guid customerId)
    {
        var booking = new Booking { Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, CustomerId = customerId, BookingReference = $"N-{Guid.NewGuid():N}"[..20], PickupAddress = "A", Destination = "B", PickupDateTime = DateTimeOffset.UtcNow, PassengerCount = 1, VehicleType = VehicleType.Saloon, BaseFare = 100, TotalFare = 100, Status = BookingStatus.Completed, PaymentStatus = "Pending", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        await using var scope = host.App.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>(); db.Bookings.Add(booking); await db.SaveChangesAsync(); return booking;
    }

    private static async Task<Notification> AddNotificationAsync(TestApiHost host, Guid companyId)
    {
        await using var scope = host.App.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        if (companyId != LondonVipCompany.Id) db.Companies.Add(new Company { Id = companyId, TradingName = "Other", LegalName = "Other", Slug = $"other-{companyId:N}", CurrencyCode = "GBP", TimeZone = "Europe/London", IsActive = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        var notification = new Notification { Id = Guid.NewGuid(), CompanyId = companyId, Recipient = "recipient@test.local", RecipientType = NotificationRecipientType.Customer, NotificationType = NotificationType.BookingCreated, Channel = NotificationChannel.Email, Subject = "Test", Body = "Test body", TemplateName = "test", Status = NotificationStatus.Pending, CreatedAt = DateTimeOffset.UtcNow, CorrelationId = Guid.NewGuid().ToString("N") };
        db.Notifications.Add(notification); await db.SaveChangesAsync(); return notification;
    }
}
