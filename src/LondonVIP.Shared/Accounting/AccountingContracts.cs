using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.Accounting;

public sealed record JournalLineRequest(Guid LedgerAccountId, string Description, decimal Debit, decimal Credit, string? Department, string? CostCentre);
public sealed record JournalRequest(string Reference, DateOnly JournalDate, string Description, string SourceType, Guid? SourceId, IReadOnlyList<JournalLineRequest> Entries);
public sealed record JournalResult(Guid Id, string Reference, JournalStatus Status, decimal Debits, decimal Credits);
public sealed record TrialBalanceLine(string Code, string Account, LedgerAccountType Type, decimal Debit, decimal Credit);
public sealed record TrialBalanceDto(DateOnly From, DateOnly To, IReadOnlyList<TrialBalanceLine> Lines, decimal TotalDebits, decimal TotalCredits);
public sealed record ExpenseRequest(string Reference, string Category, string Description, DateOnly ExpenseDate, decimal NetAmount, decimal VatAmount, Guid? DriverId, Guid? VehicleId, string? ReceiptStoragePath, string? Department, string? CostCentre);
public sealed record SupplierRequest(string SupplierNumber, string Name, string? ContactName, string? Email, string? Phone, string? Address, string? VatNumber, int PaymentTermsDays, bool IsActive);
public sealed record SupplierInvoiceRequest(Guid SupplierId, string SupplierReference, DateOnly InvoiceDate, DateOnly DueDate, decimal NetAmount, decimal VatAmount, string? Notes);
public sealed record VatReportDto(DateOnly From, DateOnly To, decimal NetSales, decimal OutputVat, decimal NetPurchases, decimal InputVat, decimal VatDue);
public sealed record DriverSettlementRequest(Guid DriverId, DateOnly PeriodStart, DateOnly PeriodEnd, decimal Bonuses, decimal Penalties, decimal Adjustments);
public sealed record FinanceDashboardDto(decimal Receivables, decimal Payables, decimal Revenue, decimal Expenses, decimal CashReceived, decimal VatDue, int OverdueInvoices, int PendingExpenses, int OpenPeriods);
public sealed record ProfitAndLossDto(DateOnly From, DateOnly To, decimal Revenue, decimal CostOfSales, decimal OperatingExpenses, decimal NetProfit);

public interface IJournalService
{
    Task<JournalResult> CreateAsync(JournalRequest request, CancellationToken token = default);
    Task<JournalResult?> PostAsync(Guid id, CancellationToken token = default);
    Task<TrialBalanceDto> TrialBalanceAsync(DateOnly from, DateOnly to, CancellationToken token = default);
}
public interface IAccountingReportService
{
    Task<FinanceDashboardDto> DashboardAsync(CancellationToken token = default);
    Task<VatReportDto> VatAsync(DateOnly from, DateOnly to, CancellationToken token = default);
    Task<ProfitAndLossDto> ProfitAndLossAsync(DateOnly from, DateOnly to, CancellationToken token = default);
}
public interface IDriverSettlementService
{
    Task<DriverSettlement> CreateAsync(DriverSettlementRequest request, CancellationToken token = default);
}
