namespace LondonVIP.Shared.Security;

public sealed class AuthSessionDto
{
    public string UserId { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public string Email { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = [];
}
