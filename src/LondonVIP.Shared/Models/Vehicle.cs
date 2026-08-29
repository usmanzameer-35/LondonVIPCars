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

    public string? Colour { get; set; }
    public int? Year { get; set; }
    public string? Notes { get; set; }
    public DateOnly? MOTExpiry { get; set; }
    public DateOnly? InsuranceExpiry { get; set; }
    public DateOnly? PrivateHireLicenceExpiry { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
