namespace LondonVIP.Shared.Models;

public enum LedgerAccountType { Asset, Liability, Equity, Revenue, Expense }
public enum JournalStatus { Draft, Posted, Reversed }
public enum AccountingPeriodStatus { Open, Closed }
public enum PayableStatus { Draft, Approved, PartiallyPaid, Paid, Overdue, Cancelled }
public enum ExpenseStatus { Draft, Submitted, Approved, Rejected, Paid }
public enum BankTransactionType { Deposit, Withdrawal, Transfer, Fee, Interest }
public enum ReconciliationStatus { Unmatched, Matched, Reconciled }
public enum VatTreatment { Standard, Reduced, ZeroRated, Exempt, ReverseCharge, OutsideScope }
public enum SettlementStatus { Draft, Approved, Paid, Cancelled }

public sealed class LedgerAccount
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public string Code { get; set; } = string.Empty; public string Name { get; set; } = string.Empty;
    public LedgerAccountType Type { get; set; } public Guid? ParentId { get; set; } public LedgerAccount? Parent { get; set; } public bool AllowPosting { get; set; } = true;
    public bool IsActive { get; set; } = true; public decimal OpeningBalance { get; set; } public DateTimeOffset CreatedAt { get; set; }
}
public sealed class FiscalYear
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public string Name { get; set; } = string.Empty; public DateOnly StartsOn { get; set; } public DateOnly EndsOn { get; set; }
    public bool IsClosed { get; set; } public DateTimeOffset? ClosedAt { get; set; } public Guid? ClosingJournalId { get; set; } public DateTimeOffset CreatedAt { get; set; } public ICollection<AccountingPeriod> Periods { get; set; } = [];
}
public sealed class AccountingPeriod
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public Guid FiscalYearId { get; set; } public FiscalYear FiscalYear { get; set; } = null!;
    public string Name { get; set; } = string.Empty; public DateOnly StartsOn { get; set; } public DateOnly EndsOn { get; set; } public AccountingPeriodStatus Status { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
}
public sealed class Journal
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public string Reference { get; set; } = string.Empty; public DateOnly JournalDate { get; set; }
    public string Description { get; set; } = string.Empty; public JournalStatus Status { get; set; } public string SourceType { get; set; } = string.Empty; public Guid? SourceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } public DateTimeOffset? PostedAt { get; set; } public ICollection<JournalEntry> Entries { get; set; } = [];
}
public sealed class JournalEntry
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public Guid JournalId { get; set; } public Journal Journal { get; set; } = null!;
    public Guid LedgerAccountId { get; set; } public LedgerAccount LedgerAccount { get; set; } = null!; public string Description { get; set; } = string.Empty;
    public decimal Debit { get; set; } public decimal Credit { get; set; } public string? Department { get; set; } public string? CostCentre { get; set; }
}
public sealed class Supplier
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public string SupplierNumber { get; set; } = string.Empty; public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; } public string? Email { get; set; } public string? Phone { get; set; } public string? Address { get; set; } public string? VatNumber { get; set; }
    public int PaymentTermsDays { get; set; } = 30; public bool IsActive { get; set; } = true; public DateTimeOffset CreatedAt { get; set; }
}
public sealed class SupplierInvoice
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public Guid SupplierId { get; set; } public Supplier Supplier { get; set; } = null!;
    public string SupplierReference { get; set; } = string.Empty; public DateOnly InvoiceDate { get; set; } public DateOnly DueDate { get; set; } public PayableStatus Status { get; set; }
    public decimal NetAmount { get; set; } public decimal VatAmount { get; set; } public decimal TotalAmount { get; set; } public decimal AmountPaid { get; set; }
    public string? Notes { get; set; } public DateTimeOffset CreatedAt { get; set; }
}
public sealed class BankAccount
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public string Name { get; set; } = string.Empty; public string CurrencyCode { get; set; } = "GBP";
    public string? SortCodeMasked { get; set; } public string? AccountNumberMasked { get; set; } public decimal OpeningBalance { get; set; } public bool IsActive { get; set; } = true;
}
public sealed class BankTransaction
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public Guid BankAccountId { get; set; } public BankAccount BankAccount { get; set; } = null!;
    public DateOnly TransactionDate { get; set; } public BankTransactionType Type { get; set; } public string Reference { get; set; } = string.Empty; public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; } public ReconciliationStatus ReconciliationStatus { get; set; } public Guid? MatchedPaymentId { get; set; } public Guid? MatchedSupplierInvoiceId { get; set; }
    public string? ImportedStatementReference { get; set; } public DateTimeOffset CreatedAt { get; set; }
}
public sealed class Expense
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public string Reference { get; set; } = string.Empty; public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty; public DateOnly ExpenseDate { get; set; } public decimal NetAmount { get; set; } public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; } public ExpenseStatus Status { get; set; } public Guid? DriverId { get; set; } public Guid? VehicleId { get; set; }
    public string? ReceiptStoragePath { get; set; } public string? Department { get; set; } public string? CostCentre { get; set; } public DateTimeOffset CreatedAt { get; set; }
}
public sealed class VatCode
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public string Code { get; set; } = string.Empty; public string Name { get; set; } = string.Empty;
    public VatTreatment Treatment { get; set; } public decimal Rate { get; set; } public bool IsActive { get; set; } = true;
}
public sealed class VatReturn
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public DateOnly PeriodStart { get; set; } public DateOnly PeriodEnd { get; set; }
    public decimal OutputVat { get; set; } public decimal InputVat { get; set; } public decimal VatDue { get; set; } public string Status { get; set; } = "Draft";
    public bool IsLocked { get; set; } public string? ProviderReference { get; set; } public DateTimeOffset CreatedAt { get; set; } public DateTimeOffset? SubmittedAt { get; set; }
}
public sealed class Budget
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public Guid FiscalYearId { get; set; } public string Department { get; set; } = string.Empty;
    public string CostCentre { get; set; } = string.Empty; public Guid LedgerAccountId { get; set; } public decimal Amount { get; set; } public decimal ForecastAmount { get; set; }
}
public sealed class DriverSettlement
{
    public Guid Id { get; set; } public Guid CompanyId { get; set; } public Guid DriverId { get; set; } public Driver Driver { get; set; } = null!;
    public string Reference { get; set; } = string.Empty; public DateOnly PeriodStart { get; set; } public DateOnly PeriodEnd { get; set; } public SettlementStatus Status { get; set; }
    public decimal GrossFares { get; set; } public decimal Commission { get; set; } public decimal Bonuses { get; set; } public decimal Penalties { get; set; }
    public decimal Adjustments { get; set; } public decimal NetPayable { get; set; } public DateTimeOffset CreatedAt { get; set; } public DateTimeOffset? PaidAt { get; set; }
}
