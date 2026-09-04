namespace LondonVIP.Shared.Models;

public enum AccountingEventType { BookingCreated, BookingCancelled, BookingCompleted, InvoiceIssued, InvoicePaid, InvoiceWrittenOff, CreditNote, Refund, PaymentAllocation, ExpenseApproval, SupplierInvoice, SupplierPayment, DriverSettlement, BankReconciliation, VatAdjustment, ManualAdjustment, RecurringInvoiceGeneration }
public enum BankMatchStatus { Suggested, Reconciled, Reversed }
public enum VatSubmissionStatus { Prepared, Validated, Submitted, Accepted, Rejected, ProviderNotConfigured }

public sealed class BankTransactionMatch
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid BankTransactionId { get; set; }
    public BankTransaction BankTransaction { get; set; } = null!;
    public Guid? PaymentId { get; set; }
    public Guid? SupplierInvoiceId { get; set; }
    public Guid? LedgerAccountId { get; set; }
    public decimal Amount { get; set; }
    public ReconciliationMatchType MatchType { get; set; }
    public BankMatchStatus Status { get; set; }
    public string? Notes { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReversedAt { get; set; }
}

public sealed class VatSubmission
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid VatReturnId { get; set; }
    public VatReturn VatReturn { get; set; } = null!;
    public string ProviderKey { get; set; } = string.Empty;
    public VatSubmissionStatus Status { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string? ProviderReference { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
}

public sealed class AccountingPostingProfile
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public Guid DebitAccountId { get; set; }
    public Guid CreditAccountId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class FinanceRecordState
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string ResourceType { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
    public bool IsArchived { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class FinanceRecordHistory
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string ResourceType { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
