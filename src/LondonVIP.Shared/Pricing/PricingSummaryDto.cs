namespace LondonVIP.Shared.Pricing;

public sealed class PricingSummaryDto
{
    public int ActiveRules { get; set; }
    public int InactiveRules { get; set; }
    public int AirportsConfigured { get; set; }
    public int VehicleTypesConfigured { get; set; }
    public decimal DefaultAirportPickupSupplement { get; set; }
    public int DefaultFreeAirportWaitingMinutes { get; set; }
    public decimal DefaultWaitingChargePerHour { get; set; }
    public bool MeetAndGreetEnabled { get; set; }
    public int MinimumBookingNoticeMinutes { get; set; }
}
