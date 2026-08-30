using System.Net;
using System.Net.Http.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.Models;
using LondonVIP.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace LondonVIP.Tests;

public sealed class BookingInvoiceEndpointTests
{
    [Fact]
    public async Task CompletedBooking_IsInvoicedIdempotentlyAndTenantScoped()
    {
        await using var host = await TestApiHost.StartAsync();
        var booking = await AddBookingAsync(host.App, BookingStatus.Completed, 125m);
        var first = await host.Client.PostAsync($"/api/bookings/{booking.Id}/invoice", null);
        var second = await host.Client.PostAsync($"/api/bookings/{booking.Id}/invoice", null);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var invoice = await first.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.NotNull(invoice);
        Assert.StartsWith("LVC-", invoice!.InvoiceNumber);
        Assert.Contains(invoice.Lines, x => x.Description.Contains(booking.BookingReference, StringComparison.Ordinal));
        await using var scope = host.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        Assert.Equal(1, await db.Invoices.CountAsync(x => x.CompanyId == booking.CompanyId));
        Assert.Equal(booking.CompanyId, await db.InvoiceLines.Where(x => x.BookingId == booking.Id).Select(x => x.Invoice.CompanyId).SingleAsync());
    }

    [Theory]
    [InlineData(BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Pending)]
    public async Task IneligibleBooking_ReturnsBadRequest(BookingStatus status)
    {
        await using var host = await TestApiHost.StartAsync();
        var booking = await AddBookingAsync(host.App, status, 100m);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsync($"/api/bookings/{booking.Id}/invoice", null)).StatusCode);
    }

    [Fact]
    public async Task MissingAndCrossTenantBookings_ReturnNotFound()
    {
        await using var host = await TestApiHost.StartAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.PostAsync($"/api/bookings/{Guid.NewGuid()}/invoice", null)).StatusCode);
        var otherCompany = Guid.NewGuid();
        var booking = await AddBookingAsync(host.App, BookingStatus.Completed, 100m, otherCompany);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/bookings/{booking.Id}/invoice");
        request.Headers.Add("X-Test-Company", LondonVipCompany.Id.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task AnonymousCaller_IsRejected()
    {
        await using var host = await TestApiHost.StartAsync();
        var booking = await AddBookingAsync(host.App, BookingStatus.Completed, 100m);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/bookings/{booking.Id}/invoice");
        request.Headers.Add("X-Test-Anonymous", "true");
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.SendAsync(request)).StatusCode);
    }

    private static async Task<Booking> AddBookingAsync(WebApplication app, BookingStatus status, decimal fare, Guid? companyId = null)
    {
        var company = companyId ?? LondonVipCompany.Id;
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        var customer = new Customer { Id = Guid.NewGuid(), CompanyId = company, FirstName = "Invoice", LastName = "Customer", Email = $"{Guid.NewGuid():N}@test.local", Phone = "000", CreatedAt = DateTimeOffset.UtcNow, IsActive = true };
        if (companyId.HasValue) db.Companies.Add(new Company { Id = company, TradingName = "Other", LegalName = "Other", Slug = $"other-{company:N}", CurrencyCode = "GBP", TimeZone = "Europe/London", IsActive = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        db.Customers.Add(customer);
        var booking = new Booking { Id = Guid.NewGuid(), CompanyId = company, CustomerId = customer.Id, BookingReference = $"AUTO-{Guid.NewGuid():N}"[..20], PickupAddress = "Pickup", Destination = "Destination", PickupDateTime = DateTimeOffset.UtcNow.AddDays(1), PassengerCount = 1, VehicleType = VehicleType.Saloon, TotalFare = fare, Status = status, PaymentStatus = "Pending", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        db.Bookings.Add(booking); await db.SaveChangesAsync(); return booking;
    }

    private sealed class InvoiceResponse { public string InvoiceNumber { get; set; } = ""; public List<InvoiceLineResponse> Lines { get; set; } = []; }
    private sealed class InvoiceLineResponse { public string Description { get; set; } = ""; }
}
