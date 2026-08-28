namespace LondonVIP.Shared.Models;

public class Booking
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public Guid CustomerId { get; set; }

    public string PickupAddress { get; set; } = string.Empty;

    public string Destination { get; set; } = string.Empty;

    public DateTimeOffset PickupDateTime { get; set; }

    public int PassengerCount { get; set; }

    public int LuggageCount { get; set; }

    public VehicleType VehicleType { get; set; }

    public decimal BaseFare { get; set; }

    public decimal Extras { get; set; }

    public decimal TotalFare { get; set; }

    public Guid? DriverId { get; set; }

    public BookingStatus Status { get; set; }

    public string PaymentStatus { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
