using LondonVIP.Infrastructure.Data;
using LondonVIP.Shared.Dashboard;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LondonVIP.Infrastructure.Dashboard;

public sealed class DashboardService(LondonVIPDbContext db, ICompanyContext company, TimeProvider clock) : IDashboardService
{
    private static readonly BookingStatus[] ActiveStatuses = [BookingStatus.Assigned, BookingStatus.DriverEnRoute, BookingStatus.DriverArrived, BookingStatus.PassengerOnBoard];
    private static readonly BookingStatus[] OpenStatuses = [BookingStatus.Pending, BookingStatus.Confirmed, .. ActiveStatuses];

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken token = default)
    {
        var revenue = await GetRevenueAsync(token);
        var bookings = await GetBookingsAsync(token);
        var operations = await GetOperationsAsync(token);
        var drivers = await GetDriversAsync(token);
        var (today, tomorrow, _, _) = await BoundsAsync(token);
        var tenantBookings = db.Bookings.AsNoTracking().Where(x => x.CompanyId == company.CompanyId);
        var now = clock.GetUtcNow();
        var todayBookings = tenantBookings.Where(x => x.PickupDateTime >= today && x.PickupDateTime < tomorrow);
        var summary = new DashboardSummaryDto
        {
            TodaysBookings = await todayBookings.CountAsync(token),
            UpcomingBookings = await tenantBookings.CountAsync(x => x.PickupDateTime >= now && OpenStatuses.Contains(x.Status), token),
            ActiveJourneys = await tenantBookings.CountAsync(x => ActiveStatuses.Contains(x.Status), token),
            CompletedToday = await todayBookings.CountAsync(x => x.Status == BookingStatus.Completed, token),
            CancelledToday = await todayBookings.CountAsync(x => x.Status == BookingStatus.Cancelled, token),
            NoShows = await todayBookings.CountAsync(x => x.Status == BookingStatus.NoShow, token),
            UnableToComplete = await todayBookings.CountAsync(x => x.Status == BookingStatus.UnableToComplete, token),
            OutstandingInvoices = await db.Invoices.CountAsync(x => x.CompanyId == company.CompanyId && x.BalanceDue > 0 && x.Status != InvoiceStatus.Cancelled, token),
            TodaysRevenue = revenue.Today,
            ThisMonthRevenue = revenue.ThisMonth,
            PaymentsReceivedToday = revenue.PaymentsReceivedToday,
            QuotesAwaitingResponse = await db.Quotations.CountAsync(x => x.CompanyId == company.CompanyId && x.Status == QuoteStatus.Active && x.ExpiresAt > now, token),
            DriversAvailable = drivers.Available,
            VehiclesAvailable = drivers.VehiclesAvailable
        };
        return new DashboardDto { Summary = summary, Revenue = revenue, Bookings = bookings, Operations = operations, Drivers = drivers };
    }

    public async Task<DashboardRevenueDto> GetRevenueAsync(CancellationToken token = default)
    {
        var (today, tomorrow, month, _) = await BoundsAsync(token);
        var from = today.AddDays(-6);
        var invoices = await db.Invoices.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.Status != InvoiceStatus.Cancelled && x.InvoiceDate >= from && x.InvoiceDate < tomorrow).Select(x => new { x.InvoiceDate, x.TotalAmount }).ToListAsync(token);
        var payments = await db.Payments.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.PaymentDate >= today && x.PaymentDate < tomorrow).Select(x => x.Amount).ToListAsync(token);
        var monthValues = month < from
            ? await db.Invoices.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.Status != InvoiceStatus.Cancelled && x.InvoiceDate >= month && x.InvoiceDate < tomorrow).Select(x => x.TotalAmount).ToListAsync(token)
            : invoices.Where(x => x.InvoiceDate >= month).Select(x => x.TotalAmount).ToList();
        return new DashboardRevenueDto
        {
            Today = invoices.Where(x => x.InvoiceDate >= today).Sum(x => x.TotalAmount),
            ThisMonth = monthValues.Sum(),
            PaymentsReceivedToday = payments.Sum(),
            RevenueByDay = Days(from, 7).Select(day => new DashboardChartPointDto(day.ToString("dd MMM"), invoices.Where(x => x.InvoiceDate >= day && x.InvoiceDate < day.AddDays(1)).Sum(x => x.TotalAmount))).ToList()
        };
    }

    public async Task<DashboardBookingsDto> GetBookingsAsync(CancellationToken token = default)
    {
        var (today, tomorrow, _, _) = await BoundsAsync(token); var from = today.AddDays(-6);
        var recent = await db.Bookings.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.PickupDateTime >= from && x.PickupDateTime < tomorrow).Select(x => x.PickupDateTime).ToListAsync(token);
        var statuses = await db.Bookings.AsNoTracking().Where(x => x.CompanyId == company.CompanyId).GroupBy(x => x.Status).Select(x => new { x.Key, Count = x.Count() }).ToListAsync(token);
        var airports = await db.Bookings.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.AirportId != null).GroupBy(x => x.Airport!.Code).Select(x => new { x.Key, Count = x.Count() }).ToListAsync(token);
        var quoteCounts = await db.Quotations.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.Status != QuoteStatus.Draft).GroupBy(x => x.Status == QuoteStatus.Converted).Select(x => new { Converted = x.Key, Count = x.Count() }).ToListAsync(token);
        var totalQuotes = quoteCounts.Sum(x => x.Count); var converted = quoteCounts.Where(x => x.Converted).Sum(x => x.Count);
        return new DashboardBookingsDto
        {
            BookingsPerDay = Days(from, 7).Select(day => new DashboardChartPointDto(day.ToString("dd MMM"), recent.Count(x => x >= day && x < day.AddDays(1)))).ToList(),
            StatusDistribution = statuses.OrderBy(x => x.Key).Select(x => new DashboardChartPointDto(Display(x.Key.ToString()), x.Count)).ToList(),
            AirportBreakdown = airports.OrderByDescending(x => x.Count).Select(x => new DashboardChartPointDto(x.Key, x.Count)).ToList(),
            QuoteConversionRate = totalQuotes == 0 ? 0 : Math.Round(converted * 100m / totalQuotes, 1)
        };
    }

    public async Task<DashboardOperationsDto> GetOperationsAsync(CancellationToken token = default)
    {
        var now = clock.GetUtcNow(); var inTwoHours = now.AddHours(2);
        var bookingQuery = db.Bookings.AsNoTracking().Where(x => x.CompanyId == company.CompanyId);
        return new DashboardOperationsDto
        {
            UpcomingPickups = await ProjectBookings(bookingQuery.Where(x => x.PickupDateTime >= now && x.PickupDateTime <= inTwoHours && OpenStatuses.Contains(x.Status)).OrderBy(x => x.PickupDateTime).Take(8)).ToListAsync(token),
            LatePickups = await ProjectBookings(bookingQuery.Where(x => x.PickupDateTime < now && OpenStatuses.Contains(x.Status)).OrderBy(x => x.PickupDateTime).Take(8)).ToListAsync(token),
            DriversEnRoute = await ProjectBookings(bookingQuery.Where(x => x.Status == BookingStatus.DriverEnRoute).OrderBy(x => x.PickupDateTime).Take(8)).ToListAsync(token),
            DriversWaiting = await ProjectBookings(bookingQuery.Where(x => x.Status == BookingStatus.DriverArrived).OrderBy(x => x.PickupDateTime).Take(8)).ToListAsync(token),
            PassengerOnboard = await ProjectBookings(bookingQuery.Where(x => x.Status == BookingStatus.PassengerOnBoard).OrderBy(x => x.PickupDateTime).Take(8)).ToListAsync(token),
            OutstandingQuotations = await db.Quotations.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.Status == QuoteStatus.Active && x.ExpiresAt > now).OrderBy(x => x.ExpiresAt).Take(8).Select(x => new DashboardQuoteItemDto(x.Id, x.QuoteReference, x.Customer.FirstName + " " + x.Customer.LastName, x.ExpiresAt, x.TotalFare, x.Status)).ToListAsync(token),
            FailedNotifications = await db.Notifications.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.Status == NotificationStatus.Failed).OrderByDescending(x => x.CreatedAt).Take(8).Select(x => new DashboardNotificationItemDto(x.Id, x.Recipient, x.Subject, x.CreatedAt, x.RetryCount)).ToListAsync(token),
            UnpaidInvoices = await db.Invoices.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.BalanceDue > 0 && x.Status != InvoiceStatus.Cancelled).OrderBy(x => x.DueDate).Take(8).Select(x => new DashboardInvoiceItemDto(x.Id, x.InvoiceNumber, x.DueDate, x.BalanceDue, x.Status)).ToListAsync(token),
            RecentPayments = await db.Payments.AsNoTracking().Where(x => x.CompanyId == company.CompanyId).OrderByDescending(x => x.PaymentDate).Take(8).Select(x => new DashboardPaymentItemDto(x.Id, x.PaymentReference, x.PaymentDate, x.Amount)).ToListAsync(token)
        };
    }

    public async Task<DashboardDriversDto> GetDriversAsync(CancellationToken token = default)
    {
        var counts = await db.Drivers.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.IsActive).GroupBy(x => x.AvailabilityStatus).Select(x => new { x.Key, Count = x.Count() }).ToListAsync(token);
        var vehicles = await db.Vehicles.CountAsync(x => x.CompanyId == company.CompanyId && x.IsActive, token);
        var busyVehicles = await db.Bookings.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && ActiveStatuses.Contains(x.Status) && x.DriverId != null && x.Driver!.VehicleId != null).Select(x => x.Driver!.VehicleId!.Value).Distinct().CountAsync(token);
        int Count(DriverAvailabilityStatus status) => counts.FirstOrDefault(x => x.Key == status)?.Count ?? 0;
        return new DashboardDriversDto
        {
            Available = Count(DriverAvailabilityStatus.Available), Busy = Count(DriverAvailabilityStatus.Busy), Offline = Count(DriverAvailabilityStatus.Offline), OnBreak = Count(DriverAvailabilityStatus.OnBreak), VehiclesAvailable = Math.Max(0, vehicles - busyVehicles),
            Utilization = counts.OrderBy(x => x.Key).Select(x => new DashboardChartPointDto(Display(x.Key.ToString()), x.Count)).ToList()
        };
    }

    private static IQueryable<DashboardBookingItemDto> ProjectBookings(IQueryable<Booking> query) => query.Select(x => new DashboardBookingItemDto(x.Id, x.BookingReference, x.PickupDateTime, x.PickupAddress, x.Destination, x.Customer.FirstName + " " + x.Customer.LastName, x.Driver == null ? null : x.Driver.FirstName + " " + x.Driver.LastName, x.Status));
    private static IEnumerable<DateTimeOffset> Days(DateTimeOffset start, int count) { for (var index = 0; index < count; index++) yield return start.AddDays(index); }
    private static string Display(string value) => System.Text.RegularExpressions.Regex.Replace(value, "([a-z])([A-Z])", "$1 $2");
    private async Task<(DateTimeOffset Today, DateTimeOffset Tomorrow, DateTimeOffset Month, string Zone)> BoundsAsync(CancellationToken token)
    {
        var zoneId = await db.Companies.AsNoTracking().Where(x => x.Id == company.CompanyId).Select(x => x.TimeZone).SingleOrDefaultAsync(token) ?? "Europe/London";
        TimeZoneInfo zone; try { zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId); } catch (TimeZoneNotFoundException) { zone = TimeZoneInfo.Utc; }
        var local = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone); var date = local.Date;
        DateTimeOffset At(DateTime value) => new DateTimeOffset(value, zone.GetUtcOffset(value)).ToUniversalTime();
        return (At(date), At(date.AddDays(1)), At(new DateTime(date.Year, date.Month, 1)), zoneId);
    }
}
