using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.Dispatch;

public sealed class DriverAvailabilityDto
{
    public Guid DriverId { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public Guid? VehicleId { get; set; }
    public string? VehicleDisplay { get; set; }
    public string? RegistrationNumber { get; set; }
    public VehicleType? VehicleType { get; set; }
    public bool IsActive { get; set; }
}
