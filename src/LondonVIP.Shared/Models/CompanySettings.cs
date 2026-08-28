namespace LondonVIP.Shared.Models;

public class CompanySettings
{
    public Guid CompanyId { get; set; }
    public int MinimumBookingNoticeMinutes { get; set; }
    public int FreeAirportWaitingMinutes { get; set; }
    public decimal WaitingChargePerHour { get; set; }
    public decimal DefaultAirportPickupSupplement { get; set; }
    public bool MeetAndGreetEnabled { get; set; }
    public decimal DriverCommissionPercentage { get; set; }
    public decimal DriverWeeklySubscriptionAmount { get; set; }
    public bool VatEnabled { get; set; }
    public decimal VatRate { get; set; }
    public string InvoicePrefix { get; set; } = string.Empty;
    public string DefaultLanguage { get; set; } = string.Empty;

    public Company Company { get; set; } = null!;
}
