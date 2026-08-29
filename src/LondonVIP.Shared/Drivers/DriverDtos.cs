using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.Drivers;

public class DriverListItemDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string? DriverNumber { get; set; }
    public bool IsActive { get; set; }
    public DriverAvailabilityStatus AvailabilityStatus { get; set; }
    public Guid? VehicleId { get; set; }
    public string? VehicleDisplay { get; set; }
    public string? RegistrationNumber { get; set; }
    public ComplianceState ComplianceState { get; set; }
    public int UpcomingBookingsCount { get; set; }
}

public sealed class DriverDetailDto : DriverListItemDto
{
    public string? Notes { get; set; }
    public string? DrivingLicenceNumber { get; set; }
    public DateOnly? DrivingLicenceExpiry { get; set; }
    public string? PrivateHireLicenceNumber { get; set; }
    public DateOnly? PrivateHireLicenceExpiry { get; set; }
    public DateOnly? DBSExpiry { get; set; }
    public DateOnly? MedicalExpiry { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class DriverCreateDto
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string? DriverNumber { get; set; }
    public string? Notes { get; set; }
    public string? DrivingLicenceNumber { get; set; }
    public DateOnly? DrivingLicenceExpiry { get; set; }
    public string? PrivateHireLicenceNumber { get; set; }
    public DateOnly? PrivateHireLicenceExpiry { get; set; }
    public DateOnly? DBSExpiry { get; set; }
    public DateOnly? MedicalExpiry { get; set; }
    public DriverAvailabilityStatus AvailabilityStatus { get; set; } = DriverAvailabilityStatus.Offline;
    public Guid? VehicleId { get; set; }
    public bool IsActive { get; set; } = true;
}
public sealed class DriverUpdateDto : DriverCreateDto;
public sealed class DriverStatusUpdateDto { public bool IsActive { get; set; } }
public sealed class DriverAvailabilityUpdateDto { public DriverAvailabilityStatus AvailabilityStatus { get; set; } }
public sealed class DriverVehicleAssignmentDto { public Guid? VehicleId { get; set; } public bool Reassign { get; set; } }
