namespace LondonVIP.Shared.Models;

public class CorporateAccount
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? TradingName { get; set; }
    public string PrimaryContactName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? BillingEmail { get; set; }
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string TownCity { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public BillingTerms BillingTerms { get; set; }
    public decimal? CreditLimit { get; set; }
    // No receivables ledger exists yet. This remains system-controlled until finance modules introduce one.
    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsOnHold { get; set; }
    public bool PurchaseOrderRequired { get; set; }
    public string? DefaultPurchaseOrderReference { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
