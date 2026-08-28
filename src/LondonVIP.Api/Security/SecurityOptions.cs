namespace LondonVIP.Api.Security;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";
    public int MaxFailedAccessAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int CookieExpirationMinutes { get; set; } = 30;
    public string[] AllowedOrigins { get; set; } = [];
    public RateLimitOptions RateLimits { get; set; } = new();
    public BootstrapAdminOptions BootstrapAdmin { get; set; } = new();
}

public sealed class RateLimitOptions
{
    public int LoginPermitLimit { get; set; } = 5;
    public int PublicQuotePermitLimit { get; set; } = 20;
    public int OperationalPermitLimit { get; set; } = 120;
    public int WindowMinutes { get; set; } = 1;
}

public sealed class BootstrapAdminOptions
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
