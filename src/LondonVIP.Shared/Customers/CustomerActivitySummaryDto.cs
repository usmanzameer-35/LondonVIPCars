namespace LondonVIP.Shared.Customers;

public sealed class CustomerActivitySummaryDto
{
    public int TotalBookings { get; set; }
    public int UpcomingBookings { get; set; }
    public int CompletedBookings { get; set; }
    public decimal TotalSpend { get; set; }
    public DateTimeOffset? LastJourney { get; set; }
}
