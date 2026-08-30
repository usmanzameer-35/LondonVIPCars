using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.Dashboard;

public sealed class DashboardDto
{
    public DashboardSummaryDto Summary { get; set; } = new();
    public DashboardRevenueDto Revenue { get; set; } = new();
    public DashboardBookingsDto Bookings { get; set; } = new();
    public DashboardOperationsDto Operations { get; set; } = new();
    public DashboardDriversDto Drivers { get; set; } = new();
}

public sealed class DashboardSummaryDto
{
    public int TodaysBookings { get; set; }
    public int UpcomingBookings { get; set; }
    public int ActiveJourneys { get; set; }
    public int CompletedToday { get; set; }
    public int CancelledToday { get; set; }
    public int NoShows { get; set; }
    public int UnableToComplete { get; set; }
    public int OutstandingInvoices { get; set; }
    public decimal TodaysRevenue { get; set; }
    public decimal ThisMonthRevenue { get; set; }
    public decimal PaymentsReceivedToday { get; set; }
    public int QuotesAwaitingResponse { get; set; }
    public int DriversAvailable { get; set; }
    public int VehiclesAvailable { get; set; }
}

public sealed class DashboardRevenueDto
{
    public decimal Today { get; set; }
    public decimal ThisMonth { get; set; }
    public decimal PaymentsReceivedToday { get; set; }
    public List<DashboardChartPointDto> RevenueByDay { get; set; } = [];
}

public sealed class DashboardBookingsDto
{
    public List<DashboardChartPointDto> BookingsPerDay { get; set; } = [];
    public List<DashboardChartPointDto> StatusDistribution { get; set; } = [];
    public List<DashboardChartPointDto> AirportBreakdown { get; set; } = [];
    public decimal QuoteConversionRate { get; set; }
}

public sealed class DashboardOperationsDto
{
    public List<DashboardBookingItemDto> UpcomingPickups { get; set; } = [];
    public List<DashboardBookingItemDto> LatePickups { get; set; } = [];
    public List<DashboardBookingItemDto> DriversEnRoute { get; set; } = [];
    public List<DashboardBookingItemDto> DriversWaiting { get; set; } = [];
    public List<DashboardBookingItemDto> PassengerOnboard { get; set; } = [];
    public List<DashboardQuoteItemDto> OutstandingQuotations { get; set; } = [];
    public List<DashboardNotificationItemDto> FailedNotifications { get; set; } = [];
    public List<DashboardInvoiceItemDto> UnpaidInvoices { get; set; } = [];
    public List<DashboardPaymentItemDto> RecentPayments { get; set; } = [];
}

public sealed class DashboardDriversDto
{
    public int Available { get; set; }
    public int Busy { get; set; }
    public int Offline { get; set; }
    public int OnBreak { get; set; }
    public int VehiclesAvailable { get; set; }
    public List<DashboardChartPointDto> Utilization { get; set; } = [];
}

public sealed record DashboardChartPointDto(string Label, decimal Value);
public sealed record DashboardBookingItemDto(Guid Id, string Reference, DateTimeOffset PickupDateTime, string Pickup, string Destination, string Customer, string? Driver, BookingStatus Status);
public sealed record DashboardQuoteItemDto(Guid Id, string Reference, string Customer, DateTimeOffset ExpiresAt, decimal TotalFare, QuoteStatus Status);
public sealed record DashboardNotificationItemDto(Guid Id, string Recipient, string Subject, DateTimeOffset CreatedAt, int RetryCount);
public sealed record DashboardInvoiceItemDto(Guid Id, string Number, DateTimeOffset DueDate, decimal BalanceDue, InvoiceStatus Status);
public sealed record DashboardPaymentItemDto(Guid Id, string Reference, DateTimeOffset PaymentDate, decimal Amount);

