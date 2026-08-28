namespace LondonVIP.Shared.Models;

public class Vehicle
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public string RegistrationNumber { get; set; } = string.Empty;

    public string Make { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public VehicleType VehicleType { get; set; }

    public int PassengerCapacity { get; set; }

    public int LuggageCapacity { get; set; }

    public bool IsActive { get; set; }
}
