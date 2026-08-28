using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.Pricing;

public class QuoteRequest
{
    public string PickupAddress { get; set; } = string.Empty;

    public string Destination { get; set; } = string.Empty;

    public Guid? AirportId { get; set; }

    public VehicleType VehicleType { get; set; }

    public int PassengerCount { get; set; }

    public int LuggageCount { get; set; }

    public bool IsAirportPickup { get; set; }

    public int WaitingMinutes { get; set; }

    public bool IsMeetAndGreet { get; set; }
}
