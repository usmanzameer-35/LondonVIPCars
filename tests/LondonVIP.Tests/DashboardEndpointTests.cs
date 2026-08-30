using System.Net;
using System.Net.Http.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.Dashboard;
using LondonVIP.Shared.Models;
using LondonVIP.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LondonVIP.Tests;

public sealed class DashboardEndpointTests
{
    [Fact]
    public async Task EmptyDatabase_ReturnsEmptyDashboardAndAllSections()
    {
        await using var host = await TestApiHost.StartAsync();
        var dashboard = await host.Client.GetFromJsonAsync<DashboardDto>("/api/dashboard");
        Assert.NotNull(dashboard); Assert.Equal(0, dashboard.Summary.TodaysBookings); Assert.Empty(dashboard.Operations.UpcomingPickups); Assert.Equal(0, dashboard.Bookings.QuoteConversionRate);
        foreach (var route in new[] { "revenue", "bookings", "operations", "drivers" }) Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync($"/api/dashboard/{route}")).StatusCode);
    }

    [Fact]
    public async Task Dashboard_CalculatesCurrentTenantOperationalMetrics()
    {
        await using var host = await TestApiHost.StartAsync();
        await SeedAsync(host, LondonVipCompany.Id, "CURRENT", 120m);
        await SeedAsync(host, Guid.NewGuid(), "OTHER", 900m);
        var dashboard = await host.Client.GetFromJsonAsync<DashboardDto>("/api/dashboard");
        Assert.NotNull(dashboard); Assert.Equal(1, dashboard.Summary.TodaysBookings); Assert.Equal(1, dashboard.Summary.ActiveJourneys); Assert.Equal(1, dashboard.Summary.OutstandingInvoices); Assert.Equal(120m, dashboard.Summary.TodaysRevenue); Assert.Equal(40m, dashboard.Summary.PaymentsReceivedToday); Assert.Equal(1, dashboard.Summary.QuotesAwaitingResponse); Assert.Equal(1, dashboard.Summary.DriversAvailable); Assert.Equal(1, dashboard.Summary.VehiclesAvailable);
        Assert.Contains(dashboard.Bookings.StatusDistribution, x => x.Label == "Driver En Route" && x.Value == 1); Assert.Contains(dashboard.Operations.DriversEnRoute, x => x.Reference == "CURRENT"); Assert.DoesNotContain(dashboard.Operations.DriversEnRoute, x => x.Reference == "OTHER");
    }

    [Fact]
    public async Task Dashboard_RequiresAuthenticationAndAuditsAccess()
    {
        await using var host = await TestApiHost.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/dashboard"); request.Headers.Add("X-Test-Anonymous", "true");
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.SendAsync(request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/dashboard")).StatusCode);
        await using var scope = host.App.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        Assert.Contains(db.SecurityAuditEvents, x => x.EventType == "DashboardViewed" && x.CompanyId == LondonVipCompany.Id);
    }

    private static async Task SeedAsync(TestApiHost host, Guid companyId, string reference, decimal invoiceAmount)
    {
        var now = DateTimeOffset.UtcNow; var customerId = Guid.NewGuid(); var driverId = Guid.NewGuid(); var vehicleId = Guid.NewGuid();
        await using var scope = host.App.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        if (companyId != LondonVipCompany.Id) db.Companies.Add(new Company { Id = companyId, TradingName = reference, LegalName = reference, Slug = $"tenant-{companyId:N}", CurrencyCode = "GBP", TimeZone = "Europe/London", IsActive = true, CreatedAt = now, UpdatedAt = now });
        db.Customers.Add(new Customer { Id = customerId, CompanyId = companyId, FirstName = reference, LastName = "Customer", Email = $"{customerId:N}@test.local", Phone = "02070000000", IsActive = true, CreatedAt = now, UpdatedAt = now });
        db.Vehicles.Add(new Vehicle { Id = vehicleId, CompanyId = companyId, RegistrationNumber = reference, Make = "Test", Model = "Car", VehicleType = VehicleType.Saloon, PassengerCapacity = 4, LuggageCapacity = 2, IsActive = true, CreatedAt = now, UpdatedAt = now });
        db.Drivers.Add(new Driver { Id = driverId, CompanyId = companyId, FirstName = reference, LastName = "Driver", Email = $"driver-{driverId:N}@test.local", Phone = "02070000001", IsActive = true, AvailabilityStatus = DriverAvailabilityStatus.Available, CreatedAt = now, UpdatedAt = now });
        db.Bookings.Add(new Booking { Id = Guid.NewGuid(), CompanyId = companyId, CustomerId = customerId, DriverId = driverId, BookingReference = reference, PickupAddress = "Pickup", Destination = "Destination", PickupDateTime = now, PassengerCount = 1, VehicleType = VehicleType.Saloon, TotalFare = invoiceAmount, Status = BookingStatus.DriverEnRoute, PaymentStatus = "Pending", CreatedAt = now, UpdatedAt = now });
        db.Invoices.Add(new Invoice { Id = Guid.NewGuid(), CompanyId = companyId, CustomerId = customerId, InvoiceNumber = $"INV-{reference}", InvoiceDate = now, DueDate = now.AddDays(14), Status = InvoiceStatus.Issued, Subtotal = invoiceAmount, TotalAmount = invoiceAmount, BalanceDue = invoiceAmount, CreatedAt = now, UpdatedAt = now });
        db.Payments.Add(new Payment { Id = Guid.NewGuid(), CompanyId = companyId, CustomerId = customerId, PaymentReference = $"PAY-{reference}", PaymentDate = now, Amount = 40m, CreatedAt = now, UpdatedAt = now });
        db.Quotations.Add(new Quotation { Id = Guid.NewGuid(), CompanyId = companyId, CustomerId = customerId, QuoteReference = $"Q-{reference}", Status = QuoteStatus.Active, ExpiresAt = now.AddDays(1), PickupAddress = "Pickup", Destination = "Destination", PickupDateTime = now.AddDays(2), PassengerCount = 1, VehicleType = VehicleType.Saloon, TotalFare = 50m, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();
    }
}
