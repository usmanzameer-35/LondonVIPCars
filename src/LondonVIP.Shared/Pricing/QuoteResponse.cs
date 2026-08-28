namespace LondonVIP.Shared.Pricing;

public class QuoteResponse
{
    public bool IsConfigured { get; set; }

    public string? Message { get; set; }

    public decimal BaseFare { get; set; }

    public decimal AirportPickupSupplement { get; set; }

    public int FreeWaitingMinutes { get; set; }

    public int ChargeableWaitingMinutes { get; set; }

    public decimal WaitingChargePerHour { get; set; }

    public decimal WaitingCharge { get; set; }

    public decimal ExtrasTotal { get; set; }

    public decimal TotalFare { get; set; }
}
