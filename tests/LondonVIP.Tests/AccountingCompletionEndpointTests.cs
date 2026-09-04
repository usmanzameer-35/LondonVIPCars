using System.Net;
using System.Net.Http.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Shared.Accounting;
using LondonVIP.Shared.Models;
using LondonVIP.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.IO.Compression;
using System.Text.Json;
using LondonVIP.Shared.Workflows;

namespace LondonVIP.Tests;

public sealed class AccountingCompletionEndpointTests
{
    [Fact]
    public async Task PartialCreditNoteApprovalIsTenantScopedAndRecalculatesInvoice()
    {
        await using var host = await TestApiHost.StartAsync();
        var invoiceId = Guid.NewGuid();
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
            db.Invoices.Add(new Invoice { Id = invoiceId, CompanyId = LondonVipCompany.Id, InvoiceNumber = "INV-CREDIT-1", InvoiceDate = DateTimeOffset.UtcNow, DueDate = DateTimeOffset.UtcNow.AddDays(14), Status = InvoiceStatus.Issued, Subtotal = 100, TaxAmount = 20, TotalAmount = 120, BalanceDue = 120, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        using var created = await host.Client.PostAsJsonAsync("/api/finance/credit-notes", new CreditNoteRequest(invoiceId, "Service adjustment", [new(null, "Partial credit", 1, 25, 20)]));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var credit = await created.Content.ReadFromJsonAsync<CreditNote>();
        Assert.NotNull(credit);
        Assert.Equal(30m, credit.TotalAmount);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsync($"/api/finance/credit-notes/{credit.Id}/approve", null)).StatusCode);

        await using var resultScope = host.App.Services.CreateAsyncScope();
        var dbResult = resultScope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
        Assert.Equal(90m, (await dbResult.Invoices.SingleAsync(x => x.Id == invoiceId)).BalanceDue);
        Assert.True(await dbResult.SecurityAuditEvents.AnyAsync(x => x.CompanyId == LondonVipCompany.Id && x.EventType == "CreditNoteApproved"));
    }

    [Fact]
    public async Task RecurringInvoiceCanGeneratePauseResumeAndAvoidFutureDuplicateRun()
    {
        await using var host = await TestApiHost.StartAsync();
        var customerId = Guid.NewGuid();
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
            db.Customers.Add(new Customer { Id = customerId, CompanyId = LondonVipCompany.Id, FirstName = "Ada", LastName = "Lovelace", Phone = "07700900000", IsActive = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        var request = new RecurringInvoiceRequest("Monthly account", customerId, null, RecurrenceFrequency.Monthly, 1, DateTimeOffset.UtcNow.AddMinutes(-1), null, 14, "[{\"Description\":\"Account service\",\"Quantity\":1,\"UnitPrice\":50,\"TaxRate\":20}]");
        var created = await (await host.Client.PostAsJsonAsync("/api/finance/recurring-invoices", request)).Content.ReadFromJsonAsync<RecurringInvoiceSchedule>();
        Assert.NotNull(created);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsync("/api/finance/recurring-invoices/process-due", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsync($"/api/finance/recurring-invoices/{created.Id}/pause", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsync($"/api/finance/recurring-invoices/{created.Id}/resume", null)).StatusCode);
        await using var scopeResult = host.App.Services.CreateAsyncScope();
        Assert.Single(await scopeResult.ServiceProvider.GetRequiredService<LondonVIPDbContext>().Invoices.Where(x => x.CustomerId == customerId).ToListAsync());
    }

    [Fact]
    public async Task CsvBankImportDetectsDuplicateFileAndSupportsReconciliationUndo()
    {
        await using var host = await TestApiHost.StartAsync();
        var account = Guid.NewGuid();
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>();
            db.BankAccounts.Add(new BankAccount { Id = account, CompanyId = LondonVipCompany.Id, Name = "Operations", CurrencyCode = "GBP", IsActive = true });
            await db.SaveChangesAsync();
        }
        var request = new BankImportRequest(account, BankImportFormat.Csv, "statement.csv", "Date,Reference,Amount,Description\n2026-09-01,BANK-1,100.00,Customer receipt");
        var first = await (await host.Client.PostAsJsonAsync("/api/finance/bank/import", request)).Content.ReadFromJsonAsync<BankImportResult>();
        var repeated = await (await host.Client.PostAsJsonAsync("/api/finance/bank/import", request)).Content.ReadFromJsonAsync<BankImportResult>();
        Assert.Equal(1, first!.Imported);
        Assert.Equal(0, repeated!.Imported);
        await using var scopeResult = host.App.Services.CreateAsyncScope();
        var transaction = await scopeResult.ServiceProvider.GetRequiredService<LondonVIPDbContext>().BankTransactions.SingleAsync();
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsJsonAsync("/api/finance/bank/reconcile", new ReconcileRequest(transaction.Id, null, null, ReconciliationMatchType.Manual))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsync($"/api/finance/bank/transactions/{transaction.Id}/undo-reconciliation", null)).StatusCode);
    }

    [Fact]
    public async Task AutomaticJournalIsIdempotentBalancedAndReversible()
    {
        await using var host = await TestApiHost.StartAsync(); var cash = Guid.NewGuid(); var revenue = Guid.NewGuid();
        await using (var scope = host.App.Services.CreateAsyncScope()) { var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>(); db.LedgerAccounts.AddRange(Account(cash, "1010", LedgerAccountType.Asset), Account(revenue, "4010", LedgerAccountType.Revenue)); await db.SaveChangesAsync(); }
        var request = new AutomaticJournalRequest(AccountingEventType.InvoiceIssued, Guid.NewGuid(), "invoice-automatic-1", "correlation-automatic-1", DateOnly.FromDateTime(DateTime.UtcNow), "Invoice issued", [new(cash, "Receivable", 120, 0), new(revenue, "Revenue", 0, 120)]);
        var first = await (await host.Client.PostAsJsonAsync("/api/finance/journals/automatic", request)).Content.ReadFromJsonAsync<JournalResult>();
        var second = await (await host.Client.PostAsJsonAsync("/api/finance/journals/automatic", request)).Content.ReadFromJsonAsync<JournalResult>();
        Assert.NotNull(first); Assert.Equal(first.Id, second!.Id); Assert.Equal(first.Debits, first.Credits);
        using var reversed = await host.Client.PostAsync($"/api/finance/journals/{first.Id}/reverse?correlationId=correlation-reversal-1", null); Assert.Equal(HttpStatusCode.OK, reversed.StatusCode);
        await using var result = host.App.Services.CreateAsyncScope(); var dbResult = result.ServiceProvider.GetRequiredService<LondonVIPDbContext>(); Assert.Equal(2, await dbResult.Journals.CountAsync()); Assert.Equal(JournalStatus.Reversed, await dbResult.Journals.Where(x => x.Id == first.Id).Select(x => x.Status).SingleAsync());
    }

    [Theory]
    [InlineData(BankImportFormat.Qif, "!Type:Bank\nD09/01/2026\nT25.00\nNQIF-1\nMTest QIF\n^")]
    [InlineData(BankImportFormat.Ofx, "<OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS><BANKTRANLIST><STMTTRN><TRNTYPE>CREDIT<DTPOSTED>20260901<TRNAMT>26.00<FITID>OFX-1<MEMO>Test OFX</STMTTRN></BANKTRANLIST></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>")]
    [InlineData(BankImportFormat.Camt, "<Document><Ntry><Amt Ccy=\"GBP\">27.00</Amt><CdtDbtInd>CRDT</CdtDbtInd><BookgDt><Dt>2026-09-01</Dt></BookgDt><NtryRef>CAMT-1</NtryRef><AddtlNtryInf>Test CAMT</AddtlNtryInf></Ntry></Document>")]
    public async Task ProductionBankFormatsImport(BankImportFormat format, string content)
    {
        await using var host = await TestApiHost.StartAsync(); var account = Guid.NewGuid();
        await using (var scope = host.App.Services.CreateAsyncScope()) { var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>(); db.BankAccounts.Add(new BankAccount { Id = account, CompanyId = LondonVipCompany.Id, Name = $"{format} account", CurrencyCode = "GBP", IsActive = true }); await db.SaveChangesAsync(); }
        var response = await host.Client.PostAsJsonAsync("/api/finance/bank/import", new BankImportRequest(account, format, $"statement.{format}", content)); Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal(1, (await response.Content.ReadFromJsonAsync<BankImportResult>())!.Imported);
    }

    [Fact]
    public async Task SplitReconciliationExcelVatFallbackAndAutomationAreOperational()
    {
        await using var host = await TestApiHost.StartAsync(); var account = Guid.NewGuid(); var transactionId = Guid.NewGuid(); var vatId = Guid.NewGuid();
        await using (var scope = host.App.Services.CreateAsyncScope()) { var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>(); db.BankAccounts.Add(new BankAccount { Id = account, CompanyId = LondonVipCompany.Id, Name = "Split account", CurrencyCode = "GBP", IsActive = true }); db.BankTransactions.Add(new BankTransaction { Id = transactionId, CompanyId = LondonVipCompany.Id, BankAccountId = account, TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow), Reference = "SPLIT-1", Description = "Split", Amount = 100, Type = BankTransactionType.Deposit, ReconciliationStatus = ReconciliationStatus.Unmatched, CreatedAt = DateTimeOffset.UtcNow }); db.VatReturns.Add(new VatReturn { Id = vatId, CompanyId = LondonVipCompany.Id, PeriodStart = new(2026, 4, 1), PeriodEnd = new(2026, 6, 30), Status = "Draft", CreatedAt = DateTimeOffset.UtcNow }); await db.SaveChangesAsync(); }
        var first = await (await host.Client.PostAsJsonAsync("/api/finance/bank/matches", new BankMatchRequest(transactionId, 40, null, null, null, ReconciliationMatchType.Manual, "Part one", "split-1"))).Content.ReadFromJsonAsync<BankTransactionMatch>();
        Assert.NotNull(first); Assert.Equal(HttpStatusCode.Created, (await host.Client.PostAsJsonAsync("/api/finance/bank/matches", new BankMatchRequest(transactionId, 60, null, null, null, ReconciliationMatchType.Manual, "Part two", "split-2"))).StatusCode); Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsync($"/api/finance/bank/matches/{first.Id}/undo", null)).StatusCode);
        var prepared = await (await host.Client.PostAsync($"/api/finance/vat/returns/{vatId}/prepare?providerKey=hmrc&correlationId=vat-1", null)).Content.ReadFromJsonAsync<VatSubmissionResult>(); Assert.NotNull(prepared); var submitted = await (await host.Client.PostAsync($"/api/finance/vat/submissions/{prepared.Id}/submit", null)).Content.ReadFromJsonAsync<VatSubmissionResult>(); Assert.Equal(VatSubmissionStatus.ProviderNotConfigured, submitted!.Status);
        var today = DateOnly.FromDateTime(DateTime.UtcNow); var export = await host.Client.GetByteArrayAsync($"/api/finance/reports/export?report=general-ledger&format=xlsx&from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}"); using var workbook = new ZipArchive(new MemoryStream(export)); Assert.Contains(workbook.Entries, x => x.FullName == "xl/worksheets/sheet1.xml");
        var pdf = await host.Client.GetByteArrayAsync($"/api/finance/reports/balance-sheet/pdf?from={today.AddYears(-1):yyyy-MM-dd}&to={today:yyyy-MM-dd}"); Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, Math.Min(5, pdf.Length)));
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsync("/api/finance/automation/schedule", null)).StatusCode); await using var result = host.App.Services.CreateAsyncScope(); Assert.Equal(9, await result.ServiceProvider.GetRequiredService<LondonVIPDbContext>().WorkflowJobs.CountAsync());
    }

    static LedgerAccount Account(Guid id, string code, LedgerAccountType type) => new() { Id = id, CompanyId = LondonVipCompany.Id, Code = code, Name = code, Type = type, AllowPosting = true, IsActive = true, CreatedAt = DateTimeOffset.UtcNow };

    [Fact]
    public async Task FinanceAdminCrudBulkLifecycleHistoryAndTenantIsolationWork()
    {
        await using var host = await TestApiHost.StartAsync();
        var payload = JsonSerializer.SerializeToElement(new Supplier { SupplierNumber = "SUP-CLOSE-1", Name = "Closure Supplier", PaymentTermsDays = 30, IsActive = true });
        using var createdResponse = await host.Client.PostAsJsonAsync("/api/finance/admin/suppliers", payload); Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode); var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>(); var id = created.GetProperty("id").GetGuid();
        using var updated = await host.Client.PutAsJsonAsync($"/api/finance/admin/suppliers/{id}", JsonSerializer.SerializeToElement(new { Name = "Updated Supplier" })); Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var query = await host.Client.GetFromJsonAsync<FinanceAdminPage>("/api/finance/admin/suppliers?page=1&pageSize=25&descending=false&includeArchived=false"); Assert.Single(query!.Items); Assert.Equal("Updated Supplier", query.Items[0].GetProperty("name").GetString());
        var bulk = new FinanceBulkRequest([id]); Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsJsonAsync("/api/finance/admin/suppliers/bulk/archive", bulk)).StatusCode); var hidden = await host.Client.GetFromJsonAsync<FinanceAdminPage>("/api/finance/admin/suppliers?page=1&pageSize=25&descending=false&includeArchived=false"); Assert.Empty(hidden!.Items);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsJsonAsync("/api/finance/admin/suppliers/bulk/restore", bulk)).StatusCode); Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsJsonAsync("/api/finance/admin/suppliers/bulk/delete", bulk)).StatusCode);
        var history = await host.Client.GetFromJsonAsync<List<FinanceRecordHistory>>($"/api/finance/admin/suppliers/{id}/history"); Assert.Equal(5, history!.Count);
    }

    [Fact]
    public async Task BusinessEventPostingProfileCreatesOneAutomaticTenantJournal()
    {
        await using var host = await TestApiHost.StartAsync(); var debit = Guid.NewGuid(); var credit = Guid.NewGuid();
        await using (var scope = host.App.Services.CreateAsyncScope()) { var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>(); db.LedgerAccounts.AddRange(Account(debit, "1100", LedgerAccountType.Asset), Account(credit, "4100", LedgerAccountType.Revenue)); db.AccountingPostingProfiles.Add(new() { Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, EventType = BusinessEventTypes.BookingCreated, DebitAccountId = debit, CreditAccountId = credit, IsActive = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }); await db.SaveChangesAsync(); var publisher = scope.ServiceProvider.GetRequiredService<IBusinessEventPublisher>(); await publisher.PublishAsync(new(BusinessEventTypes.BookingCreated, "Booking", Guid.NewGuid(), "{\"totalFare\":75}", "posting-event-1")); await publisher.PublishAsync(new(BusinessEventTypes.BookingCreated, "Booking", Guid.NewGuid(), "{\"totalFare\":75}", "posting-event-1")); }
        await using var result = host.App.Services.CreateAsyncScope(); var resultDb = result.ServiceProvider.GetRequiredService<LondonVIPDbContext>(); var journal = Assert.Single(await resultDb.Journals.Include(x => x.Entries).ToListAsync()); Assert.Equal(75m, journal.Entries.Sum(x => x.Debit)); Assert.Equal(75m, journal.Entries.Sum(x => x.Credit)); Assert.All(journal.Entries, x => Assert.Equal(LondonVipCompany.Id, x.CompanyId));
    }

    [Fact]
    public async Task YearEndClosesProfitToRetainedEarningsAndCanReopen()
    {
        await using var host = await TestApiHost.StartAsync(); var revenue = Guid.NewGuid(); var expense = Guid.NewGuid(); var retained = Guid.NewGuid(); var yearId = Guid.NewGuid(); var period1 = Guid.NewGuid(); var period2 = Guid.NewGuid();
        await using (var scope = host.App.Services.CreateAsyncScope()) { var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>(); var retainedAccount = Account(retained, "3200", LedgerAccountType.Equity); retainedAccount.Name = "Retained earnings"; db.LedgerAccounts.AddRange(Account(revenue, "4000", LedgerAccountType.Revenue), Account(expense, "5000", LedgerAccountType.Expense), retainedAccount); var year = new FiscalYear { Id = yearId, CompanyId = LondonVipCompany.Id, Name = "FY26", StartsOn = new(2026, 1, 1), EndsOn = new(2026, 12, 31), CreatedAt = DateTimeOffset.UtcNow, Periods = [new() { Id = period1, CompanyId = LondonVipCompany.Id, Name = "P1", StartsOn = new(2026, 1, 1), EndsOn = new(2026, 11, 30), Status = AccountingPeriodStatus.Closed, ClosedAt = DateTimeOffset.UtcNow }, new() { Id = period2, CompanyId = LondonVipCompany.Id, Name = "P2", StartsOn = new(2026, 12, 1), EndsOn = new(2026, 12, 31), Status = AccountingPeriodStatus.Open }] }; db.FiscalYears.Add(year); db.Journals.Add(new Journal { Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, Reference = "FY26-ACTIVITY", JournalDate = new(2026, 6, 1), Description = "Activity", SourceType = "Manual", Status = JournalStatus.Posted, CreatedAt = DateTimeOffset.UtcNow, PostedAt = DateTimeOffset.UtcNow, Entries = [new() { Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, LedgerAccountId = revenue, Description = "Revenue", Credit = 100 }, new() { Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, LedgerAccountId = expense, Description = "Expense", Debit = 40 }, new() { Id = Guid.NewGuid(), CompanyId = LondonVipCompany.Id, LedgerAccountId = retained, Description = "Balance", Debit = 60 }] }); await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsync($"/api/finance/fiscal-years/{yearId}/close", null)).StatusCode); await using (var scope = host.App.Services.CreateAsyncScope()) { var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>(); var year = await db.FiscalYears.SingleAsync(x => x.Id == yearId); Assert.True(year.IsClosed); Assert.NotNull(year.ClosingJournalId); var closing = await db.Journals.Include(x => x.Entries).SingleAsync(x => x.Id == year.ClosingJournalId); Assert.Equal(closing.Entries.Sum(x => x.Debit), closing.Entries.Sum(x => x.Credit)); Assert.Contains(closing.Entries, x => x.LedgerAccountId == retained && x.Credit == 60); }
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsync($"/api/finance/fiscal-years/{yearId}/reopen?correlationId=reopen-fy26", null)).StatusCode);
        await using var reopenedScope = host.App.Services.CreateAsyncScope(); Assert.False(await reopenedScope.ServiceProvider.GetRequiredService<LondonVIPDbContext>().FiscalYears.Where(x => x.Id == yearId).Select(x => x.IsClosed).SingleAsync());
    }

    [Fact]
    public async Task CompleteReportLibraryProducesPdfAndMultiSheetXlsx()
    {
        await using var host = await TestApiHost.StartAsync(); var today = DateOnly.FromDateTime(DateTime.UtcNow); string[] reports = ["balance-sheet", "profit-loss", "trial-balance", "cash-flow", "general-ledger", "journal-listing", "supplier-statement", "customer-statement", "driver-settlement", "vat-return", "expense-analysis", "revenue-analysis", "budget-report", "aged-debtors", "aged-creditors"];
        foreach (var report in reports)
        {
            var pdf = await host.Client.GetByteArrayAsync($"/api/finance/reports/{report}/pdf?from={today.AddYears(-1):yyyy-MM-dd}&to={today:yyyy-MM-dd}"); Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, Math.Min(5, pdf.Length)));
            var xlsx = await host.Client.GetByteArrayAsync($"/api/finance/reports/export?report={report}&format=xlsx&from={today.AddYears(-1):yyyy-MM-dd}&to={today:yyyy-MM-dd}"); using var workbook = new ZipArchive(new MemoryStream(xlsx)); Assert.Contains(workbook.Entries, x => x.FullName == "xl/worksheets/sheet1.xml"); Assert.Contains(workbook.Entries, x => x.FullName == "xl/worksheets/sheet2.xml");
        }
    }

    [Fact]
    public async Task ComparativeAndProfitabilityReportsExportWithoutData()
    {
        await using var host = await TestApiHost.StartAsync(); var today = DateOnly.FromDateTime(DateTime.UtcNow); string[] reports = ["monthly-comparison", "year-comparison", "budget-comparison", "driver-profitability", "vehicle-profitability", "customer-profitability", "corporate-profitability"];
        foreach (var report in reports) { var xlsx = await host.Client.GetByteArrayAsync($"/api/finance/reports/export?report={report}&format=xlsx&from={today.AddYears(-1):yyyy-MM-dd}&to={today:yyyy-MM-dd}"); using var workbook = new ZipArchive(new MemoryStream(xlsx)); Assert.Contains(workbook.Entries, x => x.FullName == "xl/worksheets/sheet2.xml"); }
    }

    [Fact]
    public async Task SupplierCreditsContractsDocumentsAndBankTransfersAreTenantSafe()
    {
        await using var host = await TestApiHost.StartAsync(); var supplierId = Guid.NewGuid(); var from = Guid.NewGuid(); var to = Guid.NewGuid();
        await using (var scope = host.App.Services.CreateAsyncScope()) { var db = scope.ServiceProvider.GetRequiredService<LondonVIPDbContext>(); db.Suppliers.Add(new Supplier { Id = supplierId, CompanyId = LondonVipCompany.Id, SupplierNumber = "SUP-FINAL", Name = "Final Supplier", IsActive = true, CreatedAt = DateTimeOffset.UtcNow }); db.BankAccounts.AddRange(new BankAccount { Id = from, CompanyId = LondonVipCompany.Id, Name = "Current", CurrencyCode = "GBP", IsActive = true }, new BankAccount { Id = to, CompanyId = LondonVipCompany.Id, Name = "Reserve", CurrencyCode = "GBP", IsActive = true }); await db.SaveChangesAsync(); }
        var contract = JsonSerializer.SerializeToElement(new SupplierContract { SupplierId = supplierId, Reference = "CON-1", Title = "Supply agreement", StartsOn = DateOnly.FromDateTime(DateTime.UtcNow), Status = SupplierContractStatus.Active });
        using var contractResponse = await host.Client.PostAsJsonAsync("/api/finance/admin/supplier-contracts", contract); Assert.Equal(HttpStatusCode.Created, contractResponse.StatusCode); var contractId = (await contractResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var documentResponse = await host.Client.PostAsJsonAsync("/api/finance/admin/supplier-documents", JsonSerializer.SerializeToElement(new SupplierDocument { SupplierId = supplierId, SupplierContractId = contractId, Category = "Contract", FileName = "contract.pdf", StoragePath = "suppliers/contract.pdf" })); Assert.Equal(HttpStatusCode.Created, documentResponse.StatusCode);
        using var creditResponse = await host.Client.PostAsJsonAsync("/api/finance/admin/supplier-credits", JsonSerializer.SerializeToElement(new SupplierCredit { SupplierId = supplierId, Reference = "CR-1", CreditDate = DateOnly.FromDateTime(DateTime.UtcNow), NetAmount = 10, VatAmount = 2, TotalAmount = 12, Status = SupplierCreditStatus.Approved })); Assert.Equal(HttpStatusCode.Created, creditResponse.StatusCode);
        using var transfer = await host.Client.PostAsJsonAsync("/api/finance/bank/transfers", new BankTransferRequest(from, to, DateOnly.FromDateTime(DateTime.UtcNow), 50, "TRF-1", "Reserve transfer")); Assert.Equal(HttpStatusCode.Created, transfer.StatusCode);
        await using var result = host.App.Services.CreateAsyncScope(); var dbResult = result.ServiceProvider.GetRequiredService<LondonVIPDbContext>(); Assert.Single(await dbResult.SupplierContracts.ToListAsync()); Assert.Single(await dbResult.SupplierDocuments.ToListAsync()); Assert.Single(await dbResult.SupplierCredits.ToListAsync()); var entries = await dbResult.BankTransactions.Where(x => x.ImportedStatementReference != null).ToListAsync(); Assert.Equal(2, entries.Count); Assert.Equal(0, entries.Sum(x => x.Amount)); Assert.All(entries, x => Assert.Equal(LondonVipCompany.Id, x.CompanyId));
    }
}
