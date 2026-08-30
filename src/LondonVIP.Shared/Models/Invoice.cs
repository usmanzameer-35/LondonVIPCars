namespace LondonVIP.Shared.Models;

/// <summary>
/// Invoice status enumeration.
/// </summary>
public enum InvoiceStatus
{
    Draft = 0,
    Issued = 1,
    PartiallyPaid = 2,
    Paid = 3,
    Overdue = 4,
    Cancelled = 5
}

/// <summary>
/// Represents a financial invoice for bookings, tenant-owned.
/// </summary>
public class Invoice
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public Guid? CorporateAccountId { get; set; }
    public CorporateAccount? CorporateAccount { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTimeOffset InvoiceDate { get; set; }
    public DateTimeOffset DueDate { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    
    // Navigation
    public ICollection<InvoiceLine> Lines { get; set; } = [];
    public ICollection<PaymentAllocation> Allocations { get; set; } = [];
}
