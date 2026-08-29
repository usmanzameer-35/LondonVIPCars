using LondonVIP.Shared.Drivers;
using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.Vehicles;

public class VehicleListItemDto
{
    public Guid Id { get; set; }
    public string RegistrationNumber { get; set; } = "";
    public string Make { get; set; } = "";
    public string Model { get; set; } = "";
    public VehicleType VehicleType { get; set; }
    public int PassengerCapacity { get; set; }
    public int LuggageCapacity { get; set; }
    public bool IsActive { get; set; }
    public Guid? AssignedDriverId { get; set; }
    public string? AssignedDriverName { get; set; }
    public ComplianceState ComplianceState { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class VehicleDetailDto : VehicleListItemDto
{
    public string? Colour { get; set; }
    public int? Year { get; set; }
    public string? Notes { get; set; }
    public DateOnly? MOTExpiry { get; set; }
    public DateOnly? InsuranceExpiry { get; set; }
    public DateOnly? PrivateHireLicenceExpiry { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public class VehicleCreateDto
{
    public string RegistrationNumber { get; set; } = "";
    public string Make { get; set; } = "";
    public string Model { get; set; } = "";
    public VehicleType VehicleType { get; set; }
    public int PassengerCapacity { get; set; }
    public int LuggageCapacity { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Colour { get; set; }
    public int? Year { get; set; }
    public string? Notes { get; set; }
    public DateOnly? MOTExpiry { get; set; }
    public DateOnly? InsuranceExpiry { get; set; }
    public DateOnly? PrivateHireLicenceExpiry { get; set; }
}
public sealed class VehicleUpdateDto : VehicleCreateDto;
public sealed class VehicleStatusUpdateDto { public bool IsActive { get; set; } }
