using LondonVIP.Shared.Models;
using System.Text.Json;

namespace LondonVIP.Shared.Accounting;

public sealed record CreditNoteLineRequest(Guid? InvoiceLineId,string Description,decimal Quantity,decimal UnitPrice,decimal TaxRate);
public sealed record CreditNoteRequest(Guid InvoiceId,string Reason,IReadOnlyList<CreditNoteLineRequest> Lines);
public sealed record RecurringInvoiceRequest(string Name,Guid? CustomerId,Guid? CorporateAccountId,RecurrenceFrequency Frequency,int Interval,DateTimeOffset StartsAt,DateTimeOffset? EndsAt,int PaymentTermsDays,string LinesJson);
public sealed record SupplierPaymentRequest(Guid SupplierId,string Reference,DateOnly PaymentDate,decimal Amount,PaymentMethod Method,IReadOnlyDictionary<Guid,decimal> Allocations);
public sealed record BankImportRequest(Guid BankAccountId,BankImportFormat Format,string FileName,string Content);
public sealed record BankImportResult(Guid BatchId,int Imported,int Duplicates,int Failed);
public sealed record ReconcileRequest(Guid BankTransactionId,Guid? PaymentId,Guid? SupplierInvoiceId,ReconciliationMatchType MatchType);
public sealed record FiscalYearRequest(string Name,DateOnly StartsOn,DateOnly EndsOn,int PeriodCount);
public sealed record BalanceSheetSection(string Name,decimal Current,decimal Comparative);
public sealed record BalanceSheetDto(DateOnly AsAt,IReadOnlyList<BalanceSheetSection> Assets,IReadOnlyList<BalanceSheetSection> Liabilities,IReadOnlyList<BalanceSheetSection> Equity,decimal TotalAssets,decimal TotalLiabilitiesAndEquity);
public sealed record CashFlowDto(DateOnly From,DateOnly To,decimal OpeningBalance,decimal Operating,decimal Investing,decimal Financing,decimal ClosingBalance);
public sealed record FinanceExportResult(string FileName,string ContentType,byte[] Content);
public sealed record AutomaticJournalLine(Guid LedgerAccountId,string Description,decimal Debit,decimal Credit,string? Department=null,string? CostCentre=null);
public sealed record AutomaticJournalRequest(AccountingEventType EventType,Guid SourceId,string IdempotencyKey,string CorrelationId,DateOnly JournalDate,string Description,IReadOnlyList<AutomaticJournalLine> Lines);
public sealed record BankMatchRequest(Guid BankTransactionId,decimal Amount,Guid? PaymentId,Guid? SupplierInvoiceId,Guid? LedgerAccountId,ReconciliationMatchType MatchType,string? Notes,string CorrelationId);
public sealed record VatSubmissionResult(Guid Id,VatSubmissionStatus Status,string Message,string? ProviderReference);
public sealed record FinanceAdminQuery(string? Search=null,string? Sort=null,bool Descending=false,int Page=1,int PageSize=25,bool IncludeArchived=false);
public sealed record FinanceAdminPage(IReadOnlyList<JsonElement> Items,int Page,int PageSize,int Total);
public sealed record FinanceBulkRequest(IReadOnlyList<Guid> Ids,JsonElement? Changes=null);

public interface ICreditNoteService { Task<CreditNote> CreateAsync(CreditNoteRequest request,CancellationToken token=default); Task<CreditNote?> ApproveAsync(Guid id,CancellationToken token=default); }
public interface IRecurringInvoiceService { Task<RecurringInvoiceSchedule> CreateAsync(RecurringInvoiceRequest request,CancellationToken token=default); Task<bool> CommandAsync(Guid id,string action,CancellationToken token=default); Task<int> ProcessDueAsync(CancellationToken token=default); }
public interface ISupplierPaymentService { Task<SupplierPayment> CreateAsync(SupplierPaymentRequest request,CancellationToken token=default); }
public interface IBankImportService { Task<BankImportResult> ImportAsync(BankImportRequest request,CancellationToken token=default); Task<bool> ReconcileAsync(ReconcileRequest request,CancellationToken token=default); Task<bool> UndoAsync(Guid transactionId,CancellationToken token=default); }
public interface IFiscalPeriodService { Task<FiscalYear> CreateAsync(FiscalYearRequest request,CancellationToken token=default); Task<bool> SetPeriodStatusAsync(Guid periodId,bool close,CancellationToken token=default); Task<bool> CloseYearAsync(Guid fiscalYearId,CancellationToken token=default); Task<bool> ReopenYearAsync(Guid fiscalYearId,string correlationId,CancellationToken token=default); }
public interface IFinancialStatementService { Task<BalanceSheetDto> BalanceSheetAsync(DateOnly asAt,DateOnly? comparative,CancellationToken token=default); Task<CashFlowDto> CashFlowAsync(DateOnly from,DateOnly to,CancellationToken token=default); Task<FinanceExportResult> ExportAsync(string report,string format,DateOnly from,DateOnly to,CancellationToken token=default); }
public interface ITaxSubmissionProvider { string Key{get;} bool IsConfigured{get;} Task<string> SubmitAsync(VatReturn value,CancellationToken token=default); }
public interface IBankFeedProvider { string Key{get;} bool IsConfigured{get;} Task<IReadOnlyList<BankTransaction>> ImportAsync(Guid bankAccountId,DateOnly from,DateOnly to,CancellationToken token=default); }
public interface IPayrollExportProvider { string Key{get;} Task<FinanceExportResult> ExportAsync(IReadOnlyList<DriverSettlement> settlements,CancellationToken token=default); }
public interface IAutomaticJournalService { Task<JournalResult> PostAsync(AutomaticJournalRequest request,CancellationToken token=default); Task<JournalResult?> ReverseAsync(Guid journalId,string correlationId,CancellationToken token=default); }
public interface IBankReconciliationService { Task<IReadOnlyList<BankTransactionMatch>> SuggestAsync(Guid bankTransactionId,CancellationToken token=default); Task<BankTransactionMatch> MatchAsync(BankMatchRequest request,CancellationToken token=default); Task<int> BulkMatchAsync(IReadOnlyList<BankMatchRequest> requests,CancellationToken token=default); Task<bool> UndoAsync(Guid matchId,CancellationToken token=default); }
public interface IVatSubmissionService { Task<VatSubmissionResult> PrepareAsync(Guid vatReturnId,string providerKey,string correlationId,CancellationToken token=default); Task<VatSubmissionResult> SubmitAsync(Guid submissionId,CancellationToken token=default); }
public interface IFinanceAutomationService { Task<int> ScheduleAsync(CancellationToken token=default); }
public interface IFinanceAdministrationService { Task<FinanceAdminPage> QueryAsync(string resource,FinanceAdminQuery query,CancellationToken token=default);Task<JsonElement?> GetAsync(string resource,Guid id,CancellationToken token=default);Task<JsonElement>CreateAsync(string resource,JsonElement value,string correlationId,CancellationToken token=default);Task<JsonElement?>UpdateAsync(string resource,Guid id,JsonElement changes,string correlationId,CancellationToken token=default);Task<int>BulkAsync(string resource,string action,FinanceBulkRequest request,string correlationId,CancellationToken token=default);Task<IReadOnlyList<FinanceRecordHistory>>HistoryAsync(string resource,Guid id,CancellationToken token=default); }
