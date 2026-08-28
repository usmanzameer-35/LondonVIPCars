using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using LondonVIP.Infrastructure.Bookings;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.Bookings;
using LondonVIP.Shared.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LondonVIP.Tests.Infrastructure;

namespace LondonVIP.Tests;

public class BookingEndpointTests
{
    [Fact]
    public void BookingReferenceGenerator_CreatesReadableReference()
    {
        var reference = BookingReferenceGenerator.Generate(new Guid("12345678-1234-1234-1234-1234567890ab"), new DateTimeOffset(2026, 8, 28, 10, 0, 0, TimeSpan.Zero));
        Assert.Matches(new Regex("^LVC-20260828-[A-F0-9]{7}$"), reference);
    }

    [Fact]
    public async Task PostBooking_CreatesTenantBookingWithGeneratedReference()
    {
        await WithAppAndCustomerAsync(async (_, client, customer) =>
        {
            using var response = await client.PostAsJsonAsync("/api/bookings", ValidBooking(customer.Id));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var booking = await response.Content.ReadFromJsonAsync<BookingDetailDto>();
            Assert.NotNull(booking);
            Assert.StartsWith("LVC-", booking.BookingReference);
            Assert.Equal(customer.Id, booking.CustomerId);
            Assert.Equal(60m, booking.TotalFare);
        });
    }

    [Fact]
    public async Task GetBookings_ListsOnlyCurrentTenantBookings()
    {
        await WithAppAndCustomerAsync(async (app, client, customer) =>
        {
            var current = await CreateBookingAsync(client, customer.Id);
            var other = await AddOtherTenantBookingAsync(app);
            try
            {
                var bookings = await client.GetFromJsonAsync<List<BookingListItemDto>>("/api/bookings");
                Assert.NotNull(bookings);
                Assert.Contains(bookings, item => item.Id == current.Id);
                Assert.DoesNotContain(bookings, item => item.Id == other.BookingId);
            }
            finally { await RemoveOtherTenantAsync(app, other); }
        });
    }

    [Fact]
    public async Task GetBooking_ReturnsBookingDetail()
    {
        await WithAppAndCustomerAsync(async (_, client, customer) =>
        {
            var created = await CreateBookingAsync(client, customer.Id);
            var detail = await client.GetFromJsonAsync<BookingDetailDto>($"/api/bookings/{created.Id}");
            Assert.NotNull(detail);
            Assert.Equal(created.BookingReference, detail.BookingReference);
            Assert.Equal("Terminal pickup", detail.CustomerNotes);
            Assert.Equal(VehicleType.Saloon, detail.VehicleType);
        });
    }

    [Fact]
    public async Task PutBooking_UpdatesOperationalFieldsWithoutChangingReference()
    {
        await WithAppAndCustomerAsync(async (_, client, customer) =>
        {
            var created = await CreateBookingAsync(client, customer.Id);
            var update = ValidBooking(customer.Id);
            update.Destination = "Updated destination";
            update.BaseFare = 70m; update.Extras = 5m; update.TotalFare = 75m;

            using var response = await client.PutAsJsonAsync($"/api/bookings/{created.Id}", update);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var changed = await response.Content.ReadFromJsonAsync<BookingDetailDto>();
            Assert.Equal(created.BookingReference, changed?.BookingReference);
            Assert.Equal("Updated destination", changed?.Destination);
            Assert.Equal(75m, changed?.TotalFare);
        });
    }

    [Fact]
    public async Task PatchBookingStatus_UpdatesStatusOnly()
    {
        await WithAppAndCustomerAsync(async (_, client, customer) =>
        {
            var created = await CreateBookingAsync(client, customer.Id);
            using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/bookings/{created.Id}/status")
            {
                Content = JsonContent.Create(new BookingStatusUpdateDto { Status = BookingStatus.Confirmed })
            };
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var detail = await client.GetFromJsonAsync<BookingDetailDto>($"/api/bookings/{created.Id}");
            Assert.Equal(BookingStatus.Confirmed, detail?.Status);
            Assert.Equal(created.BookingReference, detail?.BookingReference);
        });
    }

    [Fact]
    public async Task PostBooking_ReturnsValidationProblemForInvalidBooking()
    {
        await WithAppAndCustomerAsync(async (_, client, _) =>
        {
            var invalid = new BookingCreateDto { PickupDateTime = DateTimeOffset.UtcNow.AddMinutes(-1), PassengerCount = 0, LuggageCount = -1, TotalFare = 10m };
            using var response = await client.PostAsJsonAsync("/api/bookings", invalid);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<ValidationResponse>();
            Assert.NotNull(problem);
            Assert.Contains("customerId", problem.Errors.Keys, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("pickupDateTime", problem.Errors.Keys, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("passengerCount", problem.Errors.Keys, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("totalFare", problem.Errors.Keys, StringComparer.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task OtherTenantBooking_ReturnsNotFoundForReadAndStatusUpdate()
    {
        await WithAppAndCustomerAsync(async (app, client, _) =>
        {
            var other = await AddOtherTenantBookingAsync(app);
            try
            {
                using var getResponse = await client.GetAsync($"/api/bookings/{other.BookingId}");
                Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
                using var patch = new HttpRequestMessage(HttpMethod.Patch, $"/api/bookings/{other.BookingId}/status") { Content = JsonContent.Create(new BookingStatusUpdateDto { Status = BookingStatus.Cancelled }) };
                using var patchResponse = await client.SendAsync(patch);
                Assert.Equal(HttpStatusCode.NotFound, patchResponse.StatusCode);
            }
            finally { await RemoveOtherTenantAsync(app, other); }
        });
    }

    private static BookingUpdateDto ValidBooking(Guid customerId) => new()
    {
        CustomerId = customerId,
        PickupAddress = "Heathrow Terminal 5",
        Destination = "Mayfair, London",
        PickupDateTime = DateTimeOffset.UtcNow.AddDays(2),
        PassengerCount = 2,
        LuggageCount = 2,
        VehicleType = VehicleType.Saloon,
        CustomerNotes = "Terminal pickup",
        BaseFare = 50m,
        Extras = 10m,
        TotalFare = 60m,
        Status = BookingStatus.Pending,
        PaymentStatus = "Pending"
    };

    private static async Task<BookingDetailDto> CreateBookingAsync(HttpClient client, Guid customerId)
    {
        using var response = await client.PostAsJsonAsync("/api/bookings", ValidBooking(customerId));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BookingDetailDto>())!;
    }

    private static async Task WithAppAndCustomerAsync(Func<WebApplication, HttpClient, Customer, Task> test)
    {
        await using var host = await TestApiHost.StartAsync();
        var app = host.App;
        var customer = new Customer { Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, FirstName = "Booking", LastName = "Test", Email = $"{Guid.NewGuid():N}@example.test", Phone = "000", CreatedAt = DateTimeOffset.UtcNow, IsActive = true };
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
            db.Customers.Add(customer); await db.SaveChangesAsync();
        }
        try
        {
            await test(app, host.Client, customer);
        }
        finally
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
            await db.Bookings.Where(item => item.CustomerId == customer.Id).ExecuteDeleteAsync();
            await db.Customers.Where(item => item.Id == customer.Id).ExecuteDeleteAsync();
        }
    }

    private static async Task<OtherTenantData> AddOtherTenantBookingAsync(WebApplication app)
    {
        var companyId = Guid.NewGuid(); var customerId = Guid.NewGuid(); var bookingId = Guid.NewGuid();
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        db.Companies.Add(new Company { Id = companyId, TradingName = "Other Test Cars", LegalName = "Other Test Cars", Slug = $"other-{companyId:N}", Email = "other@example.test", Phone = "000", AddressLine1 = "Test", City = "London", Postcode = "TEST", Country = "UK", TimeZone = "Europe/London", CurrencyCode = "GBP", IsActive = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        db.Customers.Add(new Customer { Id = customerId, CompanyId = companyId, FirstName = "Other", LastName = "Customer", Email = $"{customerId:N}@example.test", Phone = "000", CreatedAt = DateTimeOffset.UtcNow, IsActive = true });
        db.Bookings.Add(new Booking { Id = bookingId, BookingReference = $"OTHER-{bookingId:N}"[..20], CompanyId = companyId, CustomerId = customerId, PickupAddress = "Other pickup", Destination = "Other destination", PickupDateTime = DateTimeOffset.UtcNow.AddDays(3), PassengerCount = 1, VehicleType = VehicleType.Saloon, TotalFare = 0, PaymentStatus = "Pending", Status = BookingStatus.Pending, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        return new OtherTenantData(companyId, customerId, bookingId);
    }

    private static async Task RemoveOtherTenantAsync(WebApplication app, OtherTenantData data)
    {
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        await db.Bookings.Where(item => item.Id == data.BookingId).ExecuteDeleteAsync();
        await db.Customers.Where(item => item.Id == data.CustomerId).ExecuteDeleteAsync();
        await db.Companies.Where(item => item.Id == data.CompanyId).ExecuteDeleteAsync();
    }

    private sealed record OtherTenantData(Guid CompanyId, Guid CustomerId, Guid BookingId);
    private sealed class ValidationResponse { public Dictionary<string, string[]> Errors { get; set; } = []; }
}
