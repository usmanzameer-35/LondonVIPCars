using LondonVIP.Shared.Tenancy;

namespace LondonVIP.Infrastructure.Tenancy;

public static class LondonVipCompany
{
    public static readonly Guid Id = new("a26e555d-6b9b-4d9c-86b1-b0ba606a47d8");
    public const string Slug = "london-vip-cars";
}

public sealed class DefaultCompanyContext : ICompanyContext
{
    public Guid CompanyId => LondonVipCompany.Id;
}
