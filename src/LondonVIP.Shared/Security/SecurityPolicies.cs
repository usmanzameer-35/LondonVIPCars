namespace LondonVIP.Shared.Security;

public static class SecurityPolicies
{
    public const string ErpAccess = nameof(ErpAccess);
    public const string BookingOperations = nameof(BookingOperations);
    public const string DispatchOperations = nameof(DispatchOperations);
    public const string FinanceOperations = nameof(FinanceOperations);
    public const string CompanyAdministration = nameof(CompanyAdministration);
    public const string CustomerRead = nameof(CustomerRead);
    public const string CustomerWrite = nameof(CustomerWrite);
    public const string PricingRead = nameof(PricingRead);
    public const string PricingWrite = nameof(PricingWrite);
    public const string DriverFleetRead = nameof(DriverFleetRead);
    public const string DriverFleetWrite = nameof(DriverFleetWrite);
    public const string DriverOperations = nameof(DriverOperations);
    public const string CorporateAccountsRead = nameof(CorporateAccountsRead);
    public const string CorporateAccountsWrite = nameof(CorporateAccountsWrite);
    public const string CorporateAccountsFinancialWrite = nameof(CorporateAccountsFinancialWrite);
    public const string PlatformAdministration = nameof(PlatformAdministration);
    public const string DriverPortal = nameof(DriverPortal);
    public const string CustomerPortal = nameof(CustomerPortal);
}
