namespace LondonVIP.Shared.CompanySetup;

public sealed class CompanyOperationsDto
{
    public int MinimumBookingNoticeMinutes { get; set; }
    public int FreeAirportWaitingMinutes { get; set; }
    public decimal WaitingChargePerHour { get; set; }
    public decimal DefaultAirportPickupSupplement { get; set; }
    public bool MeetAndGreetEnabled { get; set; }
    public decimal DriverCommissionPercentage { get; set; }
    public decimal DriverWeeklySubscriptionAmount { get; set; }
    public string DefaultLanguage { get; set; } = string.Empty;
}
