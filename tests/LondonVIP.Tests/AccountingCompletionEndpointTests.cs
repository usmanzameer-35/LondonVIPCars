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
}
