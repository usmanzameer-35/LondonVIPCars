namespace LondonVIP.Shared.Models;

public sealed class DriverShift
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public Guid DriverId { get; set; }
    public Driver Driver { get; set; } = null!;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public DateTimeOffset? BreakStartedAt { get; set; }
    public int BreakMinutes { get; set; }
    public int JobsCompleted { get; set; }
}

public sealed class DriverJobDecline
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public Guid DriverId { get; set; }
    public Driver Driver { get; set; } = null!;
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public string Reason { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class DriverVehicleIssue
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public Guid DriverId { get; set; }
    public Driver Driver { get; set; } = null!;
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public Guid? BookingId { get; set; }
    public Booking? Booking { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
