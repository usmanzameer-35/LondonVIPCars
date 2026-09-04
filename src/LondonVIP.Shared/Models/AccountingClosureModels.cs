namespace LondonVIP.Shared.Models;

public enum SupplierCreditStatus { Draft, Approved, Applied, Cancelled }
public enum SupplierContractStatus { Draft, Active, Expired, Terminated }

public sealed class SupplierCredit
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public Guid? SupplierInvoiceId { get; set; }
    public SupplierInvoice? SupplierInvoice { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateOnly CreditDate { get; set; }
    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountApplied { get; set; }
    public SupplierCreditStatus Status { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class SupplierContract
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public string Reference { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateOnly StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    public SupplierContractStatus Status { get; set; }
    public decimal? Value { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class SupplierDocument
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public Guid? SupplierContractId { get; set; }
    public SupplierContract? SupplierContract { get; set; }
    public string Category { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public DateOnly? ExpiresOn { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public static class ExpenseCategories
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Fuel", "Parking", "Congestion Charge", "Vehicle Repairs", "Insurance",
        "Office Costs", "Utilities", "Marketing", "General Expenses"
    };
}
