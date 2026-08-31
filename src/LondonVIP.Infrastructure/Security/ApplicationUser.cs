using Microsoft.AspNetCore.Identity;

namespace LondonVIP.Infrastructure.Security;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public Guid CompanyId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public Guid? DriverId { get; set; }
}
