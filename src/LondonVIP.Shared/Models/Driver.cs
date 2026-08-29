namespace LondonVIP.Shared.Models;

public class Driver
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public Guid? VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    public bool IsActive { get; set; }

    public string? DriverNumber { get; set; }
    public string? Notes { get; set; }
    public string? DrivingLicenceNumber { get; set; }
    public DateOnly? DrivingLicenceExpiry { get; set; }
    public string? PrivateHireLicenceNumber { get; set; }
    public DateOnly? PrivateHireLicenceExpiry { get; set; }
    public DateOnly? DBSExpiry { get; set; }
    public DateOnly? MedicalExpiry { get; set; }
    public DriverAvailabilityStatus AvailabilityStatus { get; set; } = DriverAvailabilityStatus.Offline;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
