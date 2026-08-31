namespace LondonVIP.Shared.Security;

public static class SecurityRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Dispatcher = "Dispatcher";
    public const string Finance = "Finance";
    public const string Driver = "Driver";
    public const string Customer = "Customer";
    public static readonly string[] All = [SuperAdmin, Admin, Dispatcher, Finance, Driver, Customer];
}
