using System.Text.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Shared.Accounting;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Tenancy;
using LondonVIP.Shared.Workflows;
using Microsoft.EntityFrameworkCore;

namespace LondonVIP.Infrastructure.Accounting;

public sealed class AutomaticJournalService(LondonVIPDbContext db, ICompanyContext company) : IAutomaticJournalService
{
    public async Task<JournalResult> PostAsync(AutomaticJournalRequest request, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || string.IsNullOrWhiteSpace(request.CorrelationId) || request.Lines.Count < 2 || request.Lines.Sum(x => x.Debit) != request.Lines.Sum(x => x.Credit) || request.Lines.Sum(x => x.Debit) <= 0)
            throw new InvalidOperationException("Automatic journal must be balanced and have idempotency and correlation identifiers.");
        var existing = await db.AccountingJournalLinks.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.EventType == request.EventType.ToString() && x.IdempotencyKey == request.IdempotencyKey).Select(x => x.JournalId).SingleOrDefaultAsync(token);
        if (existing != Guid.Empty) return await Result(existing, token);
        var ids = request.Lines.Select(x => x.LedgerAccountId).Distinct().ToList();
        if (await db.LedgerAccounts.CountAsync(x => x.CompanyId == company.CompanyId && x.IsActive && x.AllowPosting && ids.Contains(x.Id), token) != ids.Count) throw new InvalidOperationException("A posting account is invalid.");
        if (await db.AccountingPeriods.AnyAsync(x => x.CompanyId == company.CompanyId && x.Status == AccountingPeriodStatus.Closed && x.StartsOn <= request.JournalDate && x.EndsOn >= request.JournalDate, token)) throw new InvalidOperationException("The accounting period is closed.");
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var journal = new Journal { Id = Guid.NewGuid(), CompanyId = company.CompanyId, Reference = $"AUTO-{request.EventType}-{request.IdempotencyKey}"[..Math.Min(80, $"AUTO-{request.EventType}-{request.IdempotencyKey}".Length)], JournalDate = request.JournalDate, Description = request.Description, SourceType = request.EventType.ToString(), SourceId = request.SourceId, Status = JournalStatus.Posted, CreatedAt = DateTimeOffset.UtcNow, PostedAt = DateTimeOffset.UtcNow, Entries = request.Lines.Select(x => new JournalEntry { Id = Guid.NewGuid(), CompanyId = company.CompanyId, LedgerAccountId = x.LedgerAccountId, Description = x.Description, Debit = x.Debit, Credit = x.Credit, Department = x.Department, CostCentre = x.CostCentre }).ToList() };
        db.Journals.Add(journal);
        db.AccountingJournalLinks.Add(new AccountingJournalLink { Id = Guid.NewGuid(), CompanyId = company.CompanyId, EventType = request.EventType.ToString(), IdempotencyKey = request.IdempotencyKey, CorrelationId = request.CorrelationId, SourceId = request.SourceId, JournalId = journal.Id, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Map(journal);
    }

    public async Task<JournalResult?> ReverseAsync(Guid journalId, string correlationId, CancellationToken token = default)
    {
        var link = await db.AccountingJournalLinks.SingleOrDefaultAsync(x => x.CompanyId == company.CompanyId && x.JournalId == journalId, token);
        if (link is null) return null;
        if (link.ReversalJournalId.HasValue) return await Result(link.ReversalJournalId.Value, token);
        var original = await db.Journals.Include(x => x.Entries).SingleAsync(x => x.CompanyId == company.CompanyId && x.Id == journalId, token);
        if (original.Status != JournalStatus.Posted) throw new InvalidOperationException("Only a posted journal can be reversed.");
        var reversal = new Journal { Id = Guid.NewGuid(), CompanyId = company.CompanyId, Reference = $"REV-{original.Reference}"[..Math.Min(80, $"REV-{original.Reference}".Length)], JournalDate = DateOnly.FromDateTime(DateTime.UtcNow), Description = $"Reversal: {original.Description}", SourceType = "Reversal", SourceId = original.Id, Status = JournalStatus.Posted, CreatedAt = DateTimeOffset.UtcNow, PostedAt = DateTimeOffset.UtcNow, Entries = original.Entries.Select(x => new JournalEntry { Id = Guid.NewGuid(), CompanyId = company.CompanyId, LedgerAccountId = x.LedgerAccountId, Description = $"Reversal: {x.Description}", Debit = x.Credit, Credit = x.Debit, Department = x.Department, CostCentre = x.CostCentre }).ToList() };
        db.Journals.Add(reversal); original.Status = JournalStatus.Reversed; link.ReversalJournalId = reversal.Id; link.CorrelationId = correlationId;
        await db.SaveChangesAsync(token); return Map(reversal);
    }
    async Task<JournalResult> Result(Guid id, CancellationToken token) => Map(await db.Journals.Include(x => x.Entries).SingleAsync(x => x.CompanyId == company.CompanyId && x.Id == id, token));
    static JournalResult Map(Journal x) => new(x.Id, x.Reference, x.Status, x.Entries.Sum(e => e.Debit), x.Entries.Sum(e => e.Credit));
}

public sealed class BankReconciliationService(LondonVIPDbContext db, ICompanyContext company) : IBankReconciliationService
{
    public async Task<IReadOnlyList<BankTransactionMatch>> SuggestAsync(Guid bankTransactionId, CancellationToken token = default)
    {
        var transaction = await db.BankTransactions.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == company.CompanyId && x.Id == bankTransactionId, token) ?? throw new InvalidOperationException("Bank transaction was not found.");
        var amount = Math.Abs(transaction.Amount);
        var payments = await db.Payments.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.Amount == amount).Take(10).Select(x => new BankTransactionMatch { Id = Guid.NewGuid(), CompanyId = company.CompanyId, BankTransactionId = bankTransactionId, PaymentId = x.Id, Amount = amount, MatchType = ReconciliationMatchType.Suggested, Status = BankMatchStatus.Suggested, CorrelationId = "suggestion", CreatedAt = DateTimeOffset.UtcNow }).ToListAsync(token);
        return payments;
    }
    public async Task<BankTransactionMatch> MatchAsync(BankMatchRequest request, CancellationToken token = default)
    {
        var transaction = await db.BankTransactions.SingleOrDefaultAsync(x => x.CompanyId == company.CompanyId && x.Id == request.BankTransactionId, token) ?? throw new InvalidOperationException("Bank transaction was not found.");
        var active = await db.BankTransactionMatches.Where(x => x.CompanyId == company.CompanyId && x.BankTransactionId == transaction.Id && x.Status == BankMatchStatus.Reconciled).SumAsync(x => (decimal?)x.Amount, token) ?? 0;
        if (request.Amount <= 0 || active + request.Amount > Math.Abs(transaction.Amount)) throw new InvalidOperationException("The reconciliation amount exceeds the unmatched balance.");
        if (request.PaymentId.HasValue && !await db.Payments.AnyAsync(x => x.CompanyId == company.CompanyId && x.Id == request.PaymentId, token) || request.SupplierInvoiceId.HasValue && !await db.SupplierInvoices.AnyAsync(x => x.CompanyId == company.CompanyId && x.Id == request.SupplierInvoiceId, token)) throw new InvalidOperationException("The matching resource was not found.");
        var match = new BankTransactionMatch { Id = Guid.NewGuid(), CompanyId = company.CompanyId, BankTransactionId = transaction.Id, PaymentId = request.PaymentId, SupplierInvoiceId = request.SupplierInvoiceId, LedgerAccountId = request.LedgerAccountId, Amount = request.Amount, MatchType = request.MatchType, Status = BankMatchStatus.Reconciled, Notes = request.Notes, CorrelationId = request.CorrelationId, CreatedAt = DateTimeOffset.UtcNow };
        db.Add(match); transaction.ReconciliationStatus = active + request.Amount == Math.Abs(transaction.Amount) ? ReconciliationStatus.Reconciled : ReconciliationStatus.Matched; await db.SaveChangesAsync(token); return match;
    }
    public async Task<int> BulkMatchAsync(IReadOnlyList<BankMatchRequest> requests, CancellationToken token = default) { foreach (var request in requests) await MatchAsync(request, token); return requests.Count; }
    public async Task<bool> UndoAsync(Guid matchId, CancellationToken token = default) { var match = await db.BankTransactionMatches.Include(x => x.BankTransaction).SingleOrDefaultAsync(x => x.CompanyId == company.CompanyId && x.Id == matchId, token); if (match is null) return false; match.Status = BankMatchStatus.Reversed; match.ReversedAt = DateTimeOffset.UtcNow; var remaining = await db.BankTransactionMatches.Where(x => x.CompanyId == company.CompanyId && x.BankTransactionId == match.BankTransactionId && x.Status == BankMatchStatus.Reconciled && x.Id != match.Id).SumAsync(x => (decimal?)x.Amount, token) ?? 0; match.BankTransaction.ReconciliationStatus = remaining == 0 ? ReconciliationStatus.Unmatched : ReconciliationStatus.Matched; await db.SaveChangesAsync(token); return true; }
}

public sealed class VatSubmissionService(LondonVIPDbContext db, ICompanyContext company, IEnumerable<ITaxSubmissionProvider> providers) : IVatSubmissionService
{
    public async Task<VatSubmissionResult> PrepareAsync(Guid vatReturnId, string providerKey, string correlationId, CancellationToken token = default) { var value = await db.VatReturns.SingleOrDefaultAsync(x => x.CompanyId == company.CompanyId && x.Id == vatReturnId, token) ?? throw new InvalidOperationException("VAT return was not found."); if (value.PeriodEnd < value.PeriodStart) throw new InvalidOperationException("VAT return period is invalid."); var submission = new VatSubmission { Id = Guid.NewGuid(), CompanyId = company.CompanyId, VatReturnId = value.Id, ProviderKey = providerKey, Status = VatSubmissionStatus.Validated, CorrelationId = correlationId, CreatedAt = DateTimeOffset.UtcNow }; db.Add(submission); await db.SaveChangesAsync(token); return new(submission.Id, submission.Status, "VAT submission prepared.", null); }
    public async Task<VatSubmissionResult> SubmitAsync(Guid submissionId, CancellationToken token = default) { var submission = await db.VatSubmissions.Include(x => x.VatReturn).SingleOrDefaultAsync(x => x.CompanyId == company.CompanyId && x.Id == submissionId, token) ?? throw new InvalidOperationException("VAT submission was not found."); var provider = providers.FirstOrDefault(x => x.Key.Equals(submission.ProviderKey, StringComparison.OrdinalIgnoreCase)); if (provider is null || !provider.IsConfigured) { submission.Status = VatSubmissionStatus.ProviderNotConfigured; submission.Error = "Provider Not Configured"; await db.SaveChangesAsync(token); return new(submission.Id, submission.Status, submission.Error, null); } submission.ProviderReference = await provider.SubmitAsync(submission.VatReturn, token); submission.Status = VatSubmissionStatus.Submitted; submission.SubmittedAt = DateTimeOffset.UtcNow; submission.VatReturn.IsLocked = true; submission.VatReturn.ProviderReference = submission.ProviderReference; await db.SaveChangesAsync(token); return new(submission.Id, submission.Status, "VAT return submitted.", submission.ProviderReference); }
}

public sealed class FinanceAutomationService(IBackgroundJobService jobs) : IFinanceAutomationService
{
    public async Task<int> ScheduleAsync(CancellationToken token = default)
    {
        var now = DateTimeOffset.UtcNow; var types = new[] { "Finance.RecurringInvoices", "Finance.MonthEnd", "Finance.Statements", "Finance.PaymentReminders", "Finance.VatReminders", "Finance.AgedDebtors", "Finance.AgedCreditors", "Finance.PeriodChecks", "Finance.YearEndChecks" };
        foreach (var type in types) await jobs.ScheduleAsync(new(type, JsonSerializer.Serialize(new { generatedAt = now }), now, WorkflowJobKind.Recurring, $"{type}:{now:yyyyMMdd}", 3, "Daily"), null, token);
        return types.Length;
    }
}
