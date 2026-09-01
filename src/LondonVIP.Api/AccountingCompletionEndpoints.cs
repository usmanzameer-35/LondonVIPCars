using LondonVIP.Infrastructure.Accounting;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Security;
using LondonVIP.Shared.Accounting;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Security;
using LondonVIP.Shared.Tenancy;
using LondonVIP.Shared.Integrations;
using Microsoft.EntityFrameworkCore;

namespace LondonVIP.Api;

public static class AccountingCompletionEndpoints
{
    public static IEndpointRouteBuilder MapAccountingCompletionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/finance").RequireAuthorization(SecurityPolicies.FinanceOperations).RequireRateLimiting("operations");
        group.MapGet("/credit-notes", (LondonVIPDbContext db, ICompanyContext company, CancellationToken token) => db.CreditNotes.AsNoTracking().Where(x => x.CompanyId == company.CompanyId).Include(x => x.Lines).OrderByDescending(x => x.CreditDate).ToListAsync(token));
        group.MapPost("/credit-notes", CreateCreditNote);
        group.MapPost("/credit-notes/{id:guid}/approve", ApproveCreditNote);
        group.MapGet("/recurring-invoices", (LondonVIPDbContext db, ICompanyContext company, CancellationToken token) => db.RecurringInvoiceSchedules.AsNoTracking().Where(x => x.CompanyId == company.CompanyId).OrderBy(x => x.NextRunAt).ToListAsync(token));
        group.MapPost("/recurring-invoices", CreateRecurring);
        group.MapPost("/recurring-invoices/{id:guid}/{action}", RecurringCommand);
        group.MapPost("/recurring-invoices/process-due", ProcessRecurring);
        group.MapGet("/supplier-payments", (LondonVIPDbContext db, ICompanyContext company, CancellationToken token) => db.SupplierPayments.AsNoTracking().Where(x => x.CompanyId == company.CompanyId).Include(x => x.Allocations).OrderByDescending(x => x.PaymentDate).ToListAsync(token));
        group.MapPost("/supplier-payments", CreateSupplierPayment);
        group.MapGet("/reports/aged-creditors", AgedCreditors);
        group.MapPost("/bank/import", ImportBank);
        group.MapPost("/bank/reconcile", ReconcileBank);
        group.MapPost("/bank/transactions/{id:guid}/undo-reconciliation", UndoReconciliation);
        group.MapGet("/fiscal-years", (LondonVIPDbContext db, ICompanyContext company, CancellationToken token) => db.FiscalYears.AsNoTracking().Where(x => x.CompanyId == company.CompanyId).Include(x => x.Periods).OrderByDescending(x => x.StartsOn).ToListAsync(token));
        group.MapPost("/fiscal-years", CreateFiscalYear);
        group.MapPost("/periods/{id:guid}/close", (Guid id, IFiscalPeriodService service, CancellationToken token) => SetPeriod(id, true, service, token));
        group.MapPost("/periods/{id:guid}/open", (Guid id, IFiscalPeriodService service, CancellationToken token) => SetPeriod(id, false, service, token));
        group.MapPost("/fiscal-years/{id:guid}/close", CloseFiscalYear);
        group.MapGet("/reports/balance-sheet", (DateOnly asAt, DateOnly? comparative, IFinancialStatementService service, CancellationToken token) => service.BalanceSheetAsync(asAt, comparative, token));
        group.MapGet("/reports/cash-flow", (DateOnly from, DateOnly to, IFinancialStatementService service, CancellationToken token) => service.CashFlowAsync(from, to, token));
        group.MapGet("/reports/export", ExportReport);
        group.MapGet("/payroll/export", ExportPayroll);
        group.MapGet("/vat/returns", (LondonVIPDbContext db, ICompanyContext company, CancellationToken token) => db.VatReturns.AsNoTracking().Where(x => x.CompanyId == company.CompanyId).OrderByDescending(x => x.PeriodEnd).ToListAsync(token));
        group.MapPost("/vat/returns/{id:guid}/lock", LockVatReturn);
        group.MapPost("/journals/automatic", AutomaticJournal);
        group.MapPost("/journals/{id:guid}/reverse", ReverseJournal);
        group.MapGet("/bank/transactions/{id:guid}/suggestions", BankSuggestions);
        group.MapPost("/bank/matches", CreateBankMatch);
        group.MapPost("/bank/matches/bulk", BulkBankMatch);
        group.MapPost("/bank/matches/{id:guid}/undo", UndoBankMatch);
        group.MapPost("/vat/returns/{id:guid}/prepare", PrepareVat);
        group.MapPost("/vat/submissions/{id:guid}/submit", SubmitVat);
        group.MapGet("/vat/submissions", (LondonVIPDbContext db, ICompanyContext company, CancellationToken token) => db.VatSubmissions.AsNoTracking().Where(x => x.CompanyId == company.CompanyId).OrderByDescending(x => x.CreatedAt).ToListAsync(token));
        group.MapPost("/automation/schedule", ScheduleAutomation);
        group.MapGet("/reports/{report}/pdf", FinancePdf);
        return endpoints;
    }

    static async Task<IResult> CreateCreditNote(CreditNoteRequest request, ICreditNoteService service, IAuditService audit, ICompanyContext company, CancellationToken token) => await Execute(async () => { var value = await service.CreateAsync(request, token); await WriteAudit(audit, company, "CreditNoteCreated", value.Id, token); return Results.Created($"/api/finance/credit-notes/{value.Id}", CreditNoteResult(value)); });
    static async Task<IResult> ApproveCreditNote(Guid id, ICreditNoteService service, IAuditService audit, ICompanyContext company, CancellationToken token) => await Execute(async () => { var value = await service.ApproveAsync(id, token); if (value is null) return Results.NotFound(); await WriteAudit(audit, company, "CreditNoteApproved", id, token); return Results.Ok(CreditNoteResult(value)); });
    static async Task<IResult> CreateRecurring(RecurringInvoiceRequest request, IRecurringInvoiceService service, IAuditService audit, ICompanyContext company, CancellationToken token) => await Execute(async () => { var value = await service.CreateAsync(request, token); await WriteAudit(audit, company, "RecurringInvoiceCreated", value.Id, token); return Results.Created($"/api/finance/recurring-invoices/{value.Id}", value); });
    static async Task<IResult> RecurringCommand(Guid id, string action, IRecurringInvoiceService service, IAuditService audit, ICompanyContext company, CancellationToken token) => await Execute(async () => { if (!await service.CommandAsync(id, action, token)) return Results.NotFound(); await WriteAudit(audit, company, $"RecurringInvoice{action}", id, token); return Results.Ok(); });
    static async Task<IResult> ProcessRecurring(IRecurringInvoiceService service, CancellationToken token) => Results.Ok(new { generated = await service.ProcessDueAsync(token) });
    static async Task<IResult> CreateSupplierPayment(SupplierPaymentRequest request, ISupplierPaymentService service, IAuditService audit, ICompanyContext company, CancellationToken token) => await Execute(async () => { var value = await service.CreateAsync(request, token); await WriteAudit(audit, company, "SupplierPaymentCreated", value.Id, token); return Results.Created($"/api/finance/supplier-payments/{value.Id}", value); });
    static async Task<IResult> ImportBank(BankImportRequest request, IBankImportService service, IAuditService audit, ICompanyContext company, CancellationToken token) => await Execute(async () => { var value = await service.ImportAsync(request, token); await WriteAudit(audit, company, "BankStatementImported", value.BatchId, token); return Results.Ok(value); });
    static async Task<IResult> ReconcileBank(ReconcileRequest request, IBankImportService service, IAuditService audit, ICompanyContext company, CancellationToken token) => await Execute(async () => { if (!await service.ReconcileAsync(request, token)) return Results.NotFound(); await WriteAudit(audit, company, "BankTransactionReconciled", request.BankTransactionId, token); return Results.Ok(); });
    static async Task<IResult> UndoReconciliation(Guid id, IBankImportService service, IAuditService audit, ICompanyContext company, CancellationToken token) { if (!await service.UndoAsync(id, token)) return Results.NotFound(); await WriteAudit(audit, company, "BankReconciliationUndone", id, token); return Results.Ok(); }
    static async Task<IResult> CreateFiscalYear(FiscalYearRequest request, IFiscalPeriodService service, IAuditService audit, ICompanyContext company, CancellationToken token) => await Execute(async () => { var value = await service.CreateAsync(request, token); await WriteAudit(audit, company, "FiscalYearCreated", value.Id, token); return Results.Created($"/api/finance/fiscal-years/{value.Id}", value); });
    static async Task<IResult> SetPeriod(Guid id, bool close, IFiscalPeriodService service, CancellationToken token) => await service.SetPeriodStatusAsync(id, close, token) ? Results.Ok() : Results.NotFound();
    static async Task<IResult> CloseFiscalYear(Guid id, IFiscalPeriodService service, IAuditService audit, ICompanyContext company, CancellationToken token) => await Execute(async () => { if (!await service.CloseYearAsync(id, token)) return Results.NotFound(); await WriteAudit(audit, company, "FiscalYearClosed", id, token); return Results.Ok(); });
    static Task<List<object>> AgedCreditors(LondonVIPDbContext db, ICompanyContext company, CancellationToken token) { var today = DateOnly.FromDateTime(DateTime.UtcNow); return db.SupplierInvoices.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.Status != PayableStatus.Cancelled && x.TotalAmount > x.AmountPaid).GroupBy(x => x.Supplier.Name).Select(g => (object)new { Supplier = g.Key, Current = g.Where(x => x.DueDate >= today).Sum(x => x.TotalAmount - x.AmountPaid), Overdue = g.Where(x => x.DueDate < today).Sum(x => x.TotalAmount - x.AmountPaid) }).ToListAsync(token); }
    static async Task<IResult> ExportReport(string report, string format, DateOnly from, DateOnly to, IFinancialStatementService service, CancellationToken token) { var file = await service.ExportAsync(report, format, from, to, token); return Results.File(file.Content, file.ContentType, file.FileName); }
    static async Task<IResult> ExportPayroll(LondonVIPDbContext db, ICompanyContext company, IPayrollExportProvider provider, DateOnly from, DateOnly to, CancellationToken token) { var values = await db.DriverSettlements.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.PeriodEnd >= from && x.PeriodStart <= to).ToListAsync(token); var file = await provider.ExportAsync(values, token); return Results.File(file.Content, file.ContentType, file.FileName); }
    static async Task<IResult> LockVatReturn(Guid id, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, CancellationToken token) { var value = await db.VatReturns.SingleOrDefaultAsync(x => x.CompanyId == company.CompanyId && x.Id == id, token); if (value is null) return Results.NotFound(); value.IsLocked = true; value.Status = "Locked"; await db.SaveChangesAsync(token); await WriteAudit(audit, company, "VatReturnLocked", id, token); return Results.Ok(); }
    static async Task<IResult> AutomaticJournal(AutomaticJournalRequest request, IAutomaticJournalService service, IAuditService audit, ICompanyContext company, CancellationToken token) => await Execute(async () => { var value = await service.PostAsync(request, token); await WriteAudit(audit, company, "AutomaticJournalPosted", value.Id, token); return Results.Ok(value); });
    static async Task<IResult> ReverseJournal(Guid id, string correlationId, IAutomaticJournalService service, IAuditService audit, ICompanyContext company, CancellationToken token) => await Execute(async () => { var value = await service.ReverseAsync(id, correlationId, token); if (value is null) return Results.NotFound(); await WriteAudit(audit, company, "JournalReversed", id, token); return Results.Ok(value); });
    static Task<IReadOnlyList<BankTransactionMatch>> BankSuggestions(Guid id, IBankReconciliationService service, CancellationToken token) => service.SuggestAsync(id, token);
    static async Task<IResult> CreateBankMatch(BankMatchRequest request, IBankReconciliationService service, IAuditService audit, ICompanyContext company, CancellationToken token) => await Execute(async () => { var value = await service.MatchAsync(request, token); await WriteAudit(audit, company, "BankMatchCreated", value.Id, token); return Results.Created($"/api/finance/bank/matches/{value.Id}", value); });
    static async Task<IResult> BulkBankMatch(IReadOnlyList<BankMatchRequest> requests, IBankReconciliationService service, IAuditService audit, ICompanyContext company, CancellationToken token) => await Execute(async () => { var count = await service.BulkMatchAsync(requests, token); await WriteAudit(audit, company, "BankMatchesBulkCreated", Guid.Empty, token); return Results.Ok(new { count }); });
    static async Task<IResult> UndoBankMatch(Guid id, IBankReconciliationService service, IAuditService audit, ICompanyContext company, CancellationToken token) { if (!await service.UndoAsync(id, token)) return Results.NotFound(); await WriteAudit(audit, company, "BankMatchUndone", id, token); return Results.Ok(); }
    static async Task<IResult> PrepareVat(Guid id, string providerKey, string correlationId, IVatSubmissionService service, IAuditService audit, ICompanyContext company, CancellationToken token) => await Execute(async () => { var value = await service.PrepareAsync(id, providerKey, correlationId, token); await WriteAudit(audit, company, "VatSubmissionPrepared", value.Id, token); return Results.Ok(value); });
    static async Task<IResult> SubmitVat(Guid id, IVatSubmissionService service, IAuditService audit, ICompanyContext company, CancellationToken token) => await Execute(async () => { var value = await service.SubmitAsync(id, token); await WriteAudit(audit, company, "VatSubmissionAttempted", value.Id, token); return Results.Ok(value); });
    static async Task<IResult> ScheduleAutomation(IFinanceAutomationService service, IAuditService audit, ICompanyContext company, CancellationToken token) { var count = await service.ScheduleAsync(token); await WriteAudit(audit, company, "FinanceAutomationScheduled", Guid.Empty, token); return Results.Ok(new { count }); }
    static async Task<IResult> FinancePdf(string report, DateOnly from, DateOnly to, IAccountingReportService accounting, IFinancialStatementService statements, IPdfGenerationProvider pdf, CancellationToken token)
    {
        var normalized = report.ToLowerInvariant(); object model; PdfDocumentType type;
        switch (normalized) { case "balance-sheet": model = await statements.BalanceSheetAsync(to, from.AddYears(-1), token); type = PdfDocumentType.BalanceSheet; break; case "profit-loss": model = await accounting.ProfitAndLossAsync(from, to, token); type = PdfDocumentType.ProfitAndLoss; break; case "cash-flow": model = await statements.CashFlowAsync(from, to, token); type = PdfDocumentType.CashFlow; break; case "vat": model = await accounting.VatAsync(from, to, token); type = PdfDocumentType.VatReport; break; default: return Results.BadRequest(new { Message = "Unsupported PDF report." }); }
        try { return Results.File(await pdf.GenerateAsync(type, model, token), "application/pdf", $"{normalized}-{to:yyyyMMdd}.pdf"); } catch (InvalidOperationException exception) { return Results.Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable); }
    }
    static async Task<IResult> Execute(Func<Task<IResult>> action) { try { return await action(); } catch (InvalidOperationException exception) { return Results.BadRequest(new { exception.Message }); } }
    static object CreditNoteResult(CreditNote value) => new { value.Id, value.InvoiceId, value.CreditNoteNumber, value.CreditDate, value.Status, value.Subtotal, value.TaxAmount, value.TotalAmount, value.Reason, Lines = value.Lines.Select(x => new { x.Id, x.InvoiceLineId, x.Description, x.Quantity, x.UnitPrice, x.TaxRate, x.NetAmount, x.TaxAmount, x.TotalAmount }) };
    static Task WriteAudit(IAuditService audit, ICompanyContext company, string type, Guid id, CancellationToken token) => audit.WriteAsync(type, "Finance", "Success", SecurityEventSeverity.Information, $"{type} completed.", "Finance", id.ToString(), company.CompanyId, token);
}
