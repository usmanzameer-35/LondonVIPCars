namespace LondonVIP.Shared.Models;

/// <summary>
/// Payment method enumeration.
/// </summary>
public enum PaymentMethod
{
    Cash = 0,
    BankTransfer = 1,
    Card = 2,
    Cheque = 3,
    Other = 4
}

/// <summary>
/// Represents a payment received, tenant-owned. Record-keeping only, no live processing.
/// </summary>
public class Payment
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public string PaymentReference { get; set; } = string.Empty;
    public DateTimeOffset PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;
    public string? Notes { get; set; }
    public Guid? CorporateAccountId { get; set; }
    public CorporateAccount? CorporateAccount { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    
    // Navigation
    public ICollection<PaymentAllocation> Allocations { get; set; } = [];
}
