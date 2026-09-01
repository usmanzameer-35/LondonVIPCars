namespace LondonVIP.Shared.Models;

public enum CreditNoteStatus { Draft, Submitted, Approved, Issued, Cancelled }
public enum RecurrenceFrequency { Daily, Weekly, Monthly, Quarterly, Yearly, Custom }
public enum RecurringScheduleStatus { Active, Paused, Completed, Cancelled, Failed }
public enum SupplierPaymentStatus { Draft, Approved, Processing, Paid, Failed, Cancelled }
public enum BankImportFormat { Csv, Ofx, Qif, Camt }
public enum ReconciliationMatchType { Suggested, Automatic, Manual, Forced }

public sealed class CreditNote
{
 public Guid Id{get;set;} public Guid CompanyId{get;set;} public Guid InvoiceId{get;set;} public Invoice Invoice{get;set;}=null!; public string CreditNoteNumber{get;set;}=string.Empty;
 public DateTimeOffset CreditDate{get;set;} public CreditNoteStatus Status{get;set;} public decimal Subtotal{get;set;} public decimal TaxAmount{get;set;} public decimal TotalAmount{get;set;}
 public string? Reason{get;set;} public DateTimeOffset CreatedAt{get;set;} public DateTimeOffset UpdatedAt{get;set;} public DateTimeOffset? ApprovedAt{get;set;} public ICollection<CreditNoteLine> Lines{get;set;}=[];
}
public sealed class CreditNoteLine
{
 public Guid Id{get;set;} public Guid CompanyId{get;set;} public Guid CreditNoteId{get;set;} public CreditNote CreditNote{get;set;}=null!; public Guid? InvoiceLineId{get;set;} public string Description{get;set;}=string.Empty;
 public decimal Quantity{get;set;} public decimal UnitPrice{get;set;} public decimal TaxRate{get;set;} public decimal NetAmount{get;set;} public decimal TaxAmount{get;set;} public decimal TotalAmount{get;set;}
}
public sealed class RecurringInvoiceSchedule
{
 public Guid Id{get;set;} public Guid CompanyId{get;set;} public string Name{get;set;}=string.Empty; public Guid? CustomerId{get;set;} public Guid? CorporateAccountId{get;set;}
 public RecurrenceFrequency Frequency{get;set;} public int Interval{get;set;}=1; public DateTimeOffset StartsAt{get;set;} public DateTimeOffset NextRunAt{get;set;} public DateTimeOffset? EndsAt{get;set;}
 public RecurringScheduleStatus Status{get;set;} public int PaymentTermsDays{get;set;} public string LinesJson{get;set;}="[]"; public int FailureCount{get;set;} public string? LastError{get;set;}
 public DateTimeOffset? LastRunAt{get;set;} public DateTimeOffset CreatedAt{get;set;} public DateTimeOffset UpdatedAt{get;set;}
}
public sealed class AccountingJournalLink
{
 public Guid Id{get;set;} public Guid CompanyId{get;set;} public string EventType{get;set;}=string.Empty; public string IdempotencyKey{get;set;}=string.Empty; public string CorrelationId{get;set;}=string.Empty; public Guid SourceId{get;set;} public Guid JournalId{get;set;} public Guid? ReversalJournalId{get;set;} public DateTimeOffset CreatedAt{get;set;}
}
public sealed class SupplierPayment
{
 public Guid Id{get;set;} public Guid CompanyId{get;set;} public Guid SupplierId{get;set;} public Supplier Supplier{get;set;}=null!; public string Reference{get;set;}=string.Empty; public DateOnly PaymentDate{get;set;}
 public decimal Amount{get;set;} public PaymentMethod Method{get;set;} public SupplierPaymentStatus Status{get;set;} public decimal UnallocatedAmount{get;set;} public DateTimeOffset CreatedAt{get;set;} public ICollection<SupplierPaymentAllocation> Allocations{get;set;}=[];
}
public sealed class SupplierPaymentAllocation
{
 public Guid Id{get;set;} public Guid CompanyId{get;set;} public Guid SupplierPaymentId{get;set;} public SupplierPayment SupplierPayment{get;set;}=null!; public Guid SupplierInvoiceId{get;set;} public SupplierInvoice SupplierInvoice{get;set;}=null!; public decimal Amount{get;set;} public DateTimeOffset CreatedAt{get;set;}
}
public sealed class BankImportBatch
{
 public Guid Id{get;set;} public Guid CompanyId{get;set;} public Guid BankAccountId{get;set;} public BankImportFormat Format{get;set;} public string FileName{get;set;}=string.Empty; public string Sha256{get;set;}=string.Empty;
 public int ImportedCount{get;set;} public int DuplicateCount{get;set;} public int FailedCount{get;set;} public DateTimeOffset CreatedAt{get;set;}
}
public sealed class BankRule
{
 public Guid Id{get;set;} public Guid CompanyId{get;set;} public string Name{get;set;}=string.Empty; public string? DescriptionContains{get;set;} public decimal? MinimumAmount{get;set;} public decimal? MaximumAmount{get;set;}
 public Guid? LedgerAccountId{get;set;} public string? Department{get;set;} public string? CostCentre{get;set;} public bool AutoReconcile{get;set;} public int Priority{get;set;} public bool IsActive{get;set;}=true;
}
public sealed class BankReconciliation
{
 public Guid Id{get;set;} public Guid CompanyId{get;set;} public Guid BankAccountId{get;set;} public DateOnly StatementDate{get;set;} public decimal StatementBalance{get;set;} public decimal ReconciledBalance{get;set;}
 public string Status{get;set;}="Draft"; public DateTimeOffset CreatedAt{get;set;} public DateTimeOffset? CompletedAt{get;set;}
}
public sealed class VatAdjustment
{
 public Guid Id{get;set;} public Guid CompanyId{get;set;} public Guid? VatReturnId{get;set;} public DateOnly AdjustmentDate{get;set;} public decimal Amount{get;set;} public string Reason{get;set;}=string.Empty; public DateTimeOffset CreatedAt{get;set;}
}
