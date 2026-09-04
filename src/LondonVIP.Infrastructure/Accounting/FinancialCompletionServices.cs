using System.Text;
using System.IO.Compression;
using System.Security;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Shared.Accounting;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LondonVIP.Infrastructure.Accounting;

public sealed class FiscalPeriodService(LondonVIPDbContext db, ICompanyContext company, IAutomaticJournalService journals) : IFiscalPeriodService
{
    public async Task<FiscalYear> CreateAsync(FiscalYearRequest request, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.EndsOn < request.StartsOn || request.PeriodCount is < 1 or > 53)
            throw new InvalidOperationException("Fiscal year values are invalid.");
        if (await db.FiscalYears.AnyAsync(x => x.CompanyId == company.CompanyId && x.StartsOn <= request.EndsOn && x.EndsOn >= request.StartsOn, token))
            throw new InvalidOperationException("The fiscal year overlaps an existing year.");

        var year = new FiscalYear { Id = Guid.NewGuid(), CompanyId = company.CompanyId, Name = request.Name.Trim(), StartsOn = request.StartsOn, EndsOn = request.EndsOn, CreatedAt = DateTimeOffset.UtcNow };
        var cursor = request.StartsOn;
        for (var index = 0; index < request.PeriodCount; index++)
        {
            var end = index == request.PeriodCount - 1 ? request.EndsOn : request.StartsOn.AddMonths(index + 1).AddDays(-1);
            if (end > request.EndsOn) end = request.EndsOn;
            year.Periods.Add(new AccountingPeriod { Id = Guid.NewGuid(), CompanyId = company.CompanyId, Name = $"{request.Name} P{index + 1:00}", StartsOn = cursor, EndsOn = end, Status = AccountingPeriodStatus.Open });
            cursor = end.AddDays(1);
        }
        db.Add(year);
        await db.SaveChangesAsync(token);
        return year;
    }

    public async Task<bool> SetPeriodStatusAsync(Guid periodId, bool close, CancellationToken token = default)
    {
        var period = await db.AccountingPeriods.Include(x => x.FiscalYear).SingleOrDefaultAsync(x => x.CompanyId == company.CompanyId && x.Id == periodId, token);
        if (period is null || period.FiscalYear.IsClosed) return false;
        period.Status = close ? AccountingPeriodStatus.Closed : AccountingPeriodStatus.Open;
        period.ClosedAt = close ? DateTimeOffset.UtcNow : null;
        await db.SaveChangesAsync(token);
        return true;
    }

    public async Task<bool> CloseYearAsync(Guid fiscalYearId, CancellationToken token = default)
    {
        var year = await db.FiscalYears.Include(x => x.Periods).SingleOrDefaultAsync(x => x.CompanyId == company.CompanyId && x.Id == fiscalYearId, token);
        if (year is null) return false;
        if (year.IsClosed) return true;
        var finalPeriod = year.Periods.OrderBy(x => x.EndsOn).LastOrDefault() ?? throw new InvalidOperationException("The fiscal year has no accounting periods.");
        if (year.Periods.Any(x => x.Id != finalPeriod.Id && x.Status != AccountingPeriodStatus.Closed)) throw new InvalidOperationException("All periods except the final closing period must be closed first.");
        var retained = await db.LedgerAccounts.SingleOrDefaultAsync(x => x.CompanyId == company.CompanyId && x.Type == LedgerAccountType.Equity && x.IsActive && x.Name.Contains("Retained"), token) ?? throw new InvalidOperationException("A retained earnings equity account is required.");
        var balances = await db.JournalEntries.Where(x => x.CompanyId == company.CompanyId && x.Journal.Status == JournalStatus.Posted && x.Journal.JournalDate >= year.StartsOn && x.Journal.JournalDate <= year.EndsOn && (x.LedgerAccount.Type == LedgerAccountType.Revenue || x.LedgerAccount.Type == LedgerAccountType.Expense)).GroupBy(x => new { x.LedgerAccountId, x.LedgerAccount.Name, x.LedgerAccount.Type }).Select(g => new { g.Key.LedgerAccountId, g.Key.Name, g.Key.Type, Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) }).ToListAsync(token);
        var lines = new List<AutomaticJournalLine>(); decimal debits = 0, credits = 0;
        foreach (var balance in balances) { var amount = balance.Type == LedgerAccountType.Revenue ? balance.Credit - balance.Debit : balance.Debit - balance.Credit; if (amount == 0) continue; if (balance.Type == LedgerAccountType.Revenue) { if (amount > 0) { lines.Add(new(balance.LedgerAccountId, $"Close {balance.Name}", amount, 0)); debits += amount; } else { lines.Add(new(balance.LedgerAccountId, $"Close {balance.Name}", 0, -amount)); credits += -amount; } } else if (amount > 0) { lines.Add(new(balance.LedgerAccountId, $"Close {balance.Name}", 0, amount)); credits += amount; } else { lines.Add(new(balance.LedgerAccountId, $"Close {balance.Name}", -amount, 0)); debits += -amount; } }
        if (debits != credits) { var difference = Math.Abs(debits - credits); lines.Add(debits < credits ? new(retained.Id, "Retained earnings", difference, 0) : new(retained.Id, "Retained earnings", 0, difference)); }
        if (lines.Count >= 2) { var closing = await journals.PostAsync(new(AccountingEventType.ManualAdjustment, year.Id, $"year-end:{year.Id}", $"year-end:{year.Id}", year.EndsOn, $"Year-end close {year.Name}", lines), token); year.ClosingJournalId = closing.Id; }
        foreach (var period in year.Periods) { period.Status = AccountingPeriodStatus.Closed; period.ClosedAt ??= DateTimeOffset.UtcNow; }
        year.IsClosed = true;
        year.ClosedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(token);
        return true;
    }

    public async Task<bool> ReopenYearAsync(Guid fiscalYearId, string correlationId, CancellationToken token = default)
    {
        var year = await db.FiscalYears.Include(x => x.Periods).SingleOrDefaultAsync(x => x.CompanyId == company.CompanyId && x.Id == fiscalYearId, token);
        if (year is null) return false;
        if (!year.IsClosed) return true;
        if (year.ClosingJournalId.HasValue) await journals.ReverseAsync(year.ClosingJournalId.Value, correlationId, token);
        var final = year.Periods.OrderBy(x => x.EndsOn).Last(); final.Status = AccountingPeriodStatus.Open; final.ClosedAt = null; year.IsClosed = false; year.ClosedAt = null; year.ClosingJournalId = null; await db.SaveChangesAsync(token); return true;
    }
}

public sealed class FinancialStatementService(LondonVIPDbContext db, ICompanyContext company) : IFinancialStatementService
{
    public async Task<BalanceSheetDto> BalanceSheetAsync(DateOnly asAt, DateOnly? comparative, CancellationToken token = default)
    {
        var current = await Balances(asAt, token);
        var prior = comparative.HasValue ? await Balances(comparative.Value, token) : [];
        List<BalanceSheetSection> Section(LedgerAccountType type) => current.Where(x => x.Type == type).Select(x => new BalanceSheetSection(x.Name, x.Amount, prior.FirstOrDefault(p => p.Id == x.Id)?.Amount ?? 0)).ToList();
        var assets = Section(LedgerAccountType.Asset);
        var liabilities = Section(LedgerAccountType.Liability);
        var equity = Section(LedgerAccountType.Equity);
        var profit = current.Where(x => x.Type == LedgerAccountType.Revenue).Sum(x => x.Amount) - current.Where(x => x.Type == LedgerAccountType.Expense).Sum(x => x.Amount);
        equity.Add(new("Current-year profit", profit, 0));
        return new(asAt, assets, liabilities, equity, assets.Sum(x => x.Current), liabilities.Sum(x => x.Current) + equity.Sum(x => x.Current));
    }

    public async Task<CashFlowDto> CashFlowAsync(DateOnly from, DateOnly to, CancellationToken token = default)
    {
        if (to < from) throw new InvalidOperationException("The reporting range is invalid.");
        var opening = await db.BankAccounts.Where(x => x.CompanyId == company.CompanyId).SumAsync(x => (decimal?)x.OpeningBalance, token) ?? 0;
        opening += await db.BankTransactions.Where(x => x.CompanyId == company.CompanyId && x.TransactionDate < from).SumAsync(x => (decimal?)x.Amount, token) ?? 0;
        var operating = await db.BankTransactions.Where(x => x.CompanyId == company.CompanyId && x.TransactionDate >= from && x.TransactionDate <= to && x.Type != BankTransactionType.Transfer).SumAsync(x => (decimal?)x.Amount, token) ?? 0;
        var financing = await db.BankTransactions.Where(x => x.CompanyId == company.CompanyId && x.TransactionDate >= from && x.TransactionDate <= to && x.Type == BankTransactionType.Transfer).SumAsync(x => (decimal?)x.Amount, token) ?? 0;
        return new(from, to, opening, operating, 0, financing, opening + operating + financing);
    }

    public async Task<FinanceExportResult> ExportAsync(string report, string format, DateOnly from, DateOnly to, CancellationToken token = default)
    {
        var data = await ReportRows(report, from, to, token);
        var csv = new StringBuilder(); foreach (var row in data) csv.AppendLine(string.Join(',', row.Select(Escape)));
        if (format.Equals("excel", StringComparison.OrdinalIgnoreCase) || format.Equals("xlsx", StringComparison.OrdinalIgnoreCase))
        {
            var summary = new List<string[]> { new[] { "Report", report }, new[] { "Period start", from.ToString("yyyy-MM-dd") }, new[] { "Period end", to.ToString("yyyy-MM-dd") }, new[] { "Record count", Math.Max(0, data.Count - 1).ToString() } };
            return new($"{report}-{from:yyyyMMdd}-{to:yyyyMMdd}.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", Xlsx(new Dictionary<string, IReadOnlyList<string[]>> { ["Summary"] = summary, ["Report"] = data }));
        }
        return new($"{report}-{from:yyyyMMdd}-{to:yyyyMMdd}.csv", "text/csv", Encoding.UTF8.GetBytes(csv.ToString()));
    }

    async Task<List<string[]>> ReportRows(string report, DateOnly from, DateOnly to, CancellationToken token)
    {
        var normalized = report.ToLowerInvariant();
        if (normalized is "general-ledger" or "journals" or "journal-listing") { var values = await db.JournalEntries.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.Journal.JournalDate >= from && x.Journal.JournalDate <= to).OrderBy(x => x.Journal.JournalDate).Select(x => new { x.Journal.JournalDate, x.Journal.Reference, Account = x.LedgerAccount.Code, x.Description, x.Debit, x.Credit, x.Journal.Status }).ToListAsync(token); return [["Date","Reference","Account","Description","Debit","Credit","Status"], .. values.Select(x => new[] { x.JournalDate.ToString("yyyy-MM-dd"), x.Reference, x.Account, x.Description, x.Debit.ToString("F2"), x.Credit.ToString("F2"), x.Status.ToString() })]; }
        if (normalized is "trial-balance") { var values = await db.JournalEntries.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.Journal.Status == JournalStatus.Posted && x.Journal.JournalDate >= from && x.Journal.JournalDate <= to).GroupBy(x => new { x.LedgerAccount.Code, x.LedgerAccount.Name }).Select(g => new { g.Key.Code, g.Key.Name, Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) }).OrderBy(x => x.Code).ToListAsync(token); return [["Code","Account","Debit","Credit"], .. values.Select(x => new[] { x.Code, x.Name, x.Debit.ToString("F2"), x.Credit.ToString("F2") }), ["","Totals",values.Sum(x=>x.Debit).ToString("F2"),values.Sum(x=>x.Credit).ToString("F2")]]; }
        if (normalized is "profit-loss" or "revenue-analysis") { var value = await new AccountingReportService(db, company, TimeProvider.System).ProfitAndLossAsync(from, to, token); return [["Metric","Amount"],["Revenue",value.Revenue.ToString("F2")],["Cost of sales",value.CostOfSales.ToString("F2")],["Operating expenses",value.OperatingExpenses.ToString("F2")],["Net profit",value.NetProfit.ToString("F2")]]; }
        if (normalized is "balance-sheet") { var value = await BalanceSheetAsync(to, from.AddYears(-1), token); return [["Section","Account","Current","Comparative"], .. value.Assets.Select(x=>new[]{"Assets",x.Name,x.Current.ToString("F2"),x.Comparative.ToString("F2")}), .. value.Liabilities.Select(x=>new[]{"Liabilities",x.Name,x.Current.ToString("F2"),x.Comparative.ToString("F2")}), .. value.Equity.Select(x=>new[]{"Equity",x.Name,x.Current.ToString("F2"),x.Comparative.ToString("F2")}), ["","Total assets",value.TotalAssets.ToString("F2"),""],["","Liabilities and equity",value.TotalLiabilitiesAndEquity.ToString("F2"),""]]; }
        if (normalized is "cash-flow") { var value = await CashFlowAsync(from, to, token); return [["Category","Amount"],["Opening balance",value.OpeningBalance.ToString("F2")],["Operating",value.Operating.ToString("F2")],["Investing",value.Investing.ToString("F2")],["Financing",value.Financing.ToString("F2")],["Closing balance",value.ClosingBalance.ToString("F2")]]; }
        if (normalized is "vat" or "vat-return") { var value = await new AccountingReportService(db, company, TimeProvider.System).VatAsync(from, to, token); return [["Metric","Amount"],["Net sales",value.NetSales.ToString("F2")],["Output VAT",value.OutputVat.ToString("F2")],["Net purchases",value.NetPurchases.ToString("F2")],["Input VAT",value.InputVat.ToString("F2")],["VAT due",value.VatDue.ToString("F2")]]; }
        if (normalized is "expenses" or "expense-analysis") { var values = await db.Expenses.AsNoTracking().Where(x=>x.CompanyId==company.CompanyId&&x.ExpenseDate>=from&&x.ExpenseDate<=to).GroupBy(x=>x.Category).Select(g=>new{Category=g.Key,Net=g.Sum(x=>x.NetAmount),Vat=g.Sum(x=>x.VatAmount),Total=g.Sum(x=>x.TotalAmount)}).ToListAsync(token);return [["Category","Net","VAT","Total"],..values.Select(x=>new[]{x.Category,x.Net.ToString("F2"),x.Vat.ToString("F2"),x.Total.ToString("F2")})]; }
        if (normalized is "suppliers" or "supplier-statement" or "aged-creditors") { var today=to;var values=await db.SupplierInvoices.AsNoTracking().Where(x=>x.CompanyId==company.CompanyId&&x.InvoiceDate>=from&&x.InvoiceDate<=to&&x.Status!=PayableStatus.Cancelled).Select(x=>new{x.Supplier.Name,x.SupplierReference,x.InvoiceDate,x.DueDate,x.TotalAmount,x.AmountPaid}).ToListAsync(token);return [["Supplier","Reference","Invoice date","Due date","Total","Paid","Outstanding","Age"],..values.Select(x=>new[]{x.Name,x.SupplierReference,x.InvoiceDate.ToString("yyyy-MM-dd"),x.DueDate.ToString("yyyy-MM-dd"),x.TotalAmount.ToString("F2"),x.AmountPaid.ToString("F2"),(x.TotalAmount-x.AmountPaid).ToString("F2"),(today.DayNumber-x.DueDate.DayNumber).ToString()})]; }
        if (normalized is "customer-statement" or "aged-debtors" or "customers") { var values=await db.Invoices.AsNoTracking().Where(x=>x.CompanyId==company.CompanyId&&x.InvoiceDate>=new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero)&&x.InvoiceDate<=new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue),TimeSpan.Zero)&&x.Status!=InvoiceStatus.Cancelled).Select(x=>new{Name=x.Customer==null?x.CorporateAccount!.AccountName:x.Customer.FirstName+" "+x.Customer.LastName,x.InvoiceNumber,x.InvoiceDate,x.DueDate,x.TotalAmount,x.AmountPaid,x.BalanceDue}).ToListAsync(token);return [["Customer","Invoice","Invoice date","Due date","Total","Paid","Outstanding"],..values.Select(x=>new[]{x.Name,x.InvoiceNumber,x.InvoiceDate.ToString("yyyy-MM-dd"),x.DueDate.ToString("yyyy-MM-dd"),x.TotalAmount.ToString("F2"),x.AmountPaid.ToString("F2"),x.BalanceDue.ToString("F2")})]; }
        if (normalized is "driver-settlement") { var values=await db.DriverSettlements.AsNoTracking().Where(x=>x.CompanyId==company.CompanyId&&x.PeriodEnd>=from&&x.PeriodStart<=to).Select(x=>new{Name=x.Driver.FirstName+" "+x.Driver.LastName,x.Reference,x.PeriodStart,x.PeriodEnd,x.GrossFares,x.Commission,x.NetPayable,x.Status}).ToListAsync(token);return [["Driver","Reference","From","To","Gross","Commission","Net","Status"],..values.Select(x=>new[]{x.Name,x.Reference,x.PeriodStart.ToString("yyyy-MM-dd"),x.PeriodEnd.ToString("yyyy-MM-dd"),x.GrossFares.ToString("F2"),x.Commission.ToString("F2"),x.NetPayable.ToString("F2"),x.Status.ToString()})]; }
        if (normalized is "budget" or "budget-report") { var values=await db.Budgets.AsNoTracking().Where(x=>x.CompanyId==company.CompanyId).Select(x=>new{x.Department,x.CostCentre,Account=x.LedgerAccountId,x.Amount,x.ForecastAmount}).ToListAsync(token);return [["Department","Cost centre","Account","Budget","Forecast"],..values.Select(x=>new[]{x.Department,x.CostCentre,x.Account.ToString(),x.Amount.ToString("F2"),x.ForecastAmount.ToString("F2")})]; }
        if (normalized is "monthly-comparison") { var values=await db.JournalEntries.AsNoTracking().Where(x=>x.CompanyId==company.CompanyId&&x.Journal.Status==JournalStatus.Posted&&x.Journal.JournalDate>=from&&x.Journal.JournalDate<=to).GroupBy(x=>new{x.Journal.JournalDate.Year,x.Journal.JournalDate.Month}).Select(g=>new{g.Key.Year,g.Key.Month,Revenue=g.Where(x=>x.LedgerAccount.Type==LedgerAccountType.Revenue).Sum(x=>x.Credit-x.Debit),Expenses=g.Where(x=>x.LedgerAccount.Type==LedgerAccountType.Expense).Sum(x=>x.Debit-x.Credit)}).OrderBy(x=>x.Year).ThenBy(x=>x.Month).ToListAsync(token);return [["Month","Revenue","Expenses","Profit"],..values.Select(x=>new[]{$"{x.Year:D4}-{x.Month:D2}",x.Revenue.ToString("F2"),x.Expenses.ToString("F2"),(x.Revenue-x.Expenses).ToString("F2")})]; }
        if (normalized is "year-comparison") { var values=await db.JournalEntries.AsNoTracking().Where(x=>x.CompanyId==company.CompanyId&&x.Journal.Status==JournalStatus.Posted&&x.Journal.JournalDate>=from&&x.Journal.JournalDate<=to).GroupBy(x=>x.Journal.JournalDate.Year).Select(g=>new{Year=g.Key,Revenue=g.Where(x=>x.LedgerAccount.Type==LedgerAccountType.Revenue).Sum(x=>x.Credit-x.Debit),Expenses=g.Where(x=>x.LedgerAccount.Type==LedgerAccountType.Expense).Sum(x=>x.Debit-x.Credit)}).OrderBy(x=>x.Year).ToListAsync(token);return [["Year","Revenue","Expenses","Profit"],..values.Select(x=>new[]{x.Year.ToString(),x.Revenue.ToString("F2"),x.Expenses.ToString("F2"),(x.Revenue-x.Expenses).ToString("F2")})]; }
        if (normalized is "budget-comparison" or "budget-variance") { var values=await db.Budgets.AsNoTracking().Where(x=>x.CompanyId==company.CompanyId).Select(x=>new{x.Department,x.CostCentre,Account=db.LedgerAccounts.Where(a=>a.CompanyId==company.CompanyId&&a.Id==x.LedgerAccountId).Select(a=>a.Code).First(),x.Amount,x.ForecastAmount,Actual=db.JournalEntries.Where(e=>e.CompanyId==company.CompanyId&&e.LedgerAccountId==x.LedgerAccountId&&e.Journal.Status==JournalStatus.Posted&&e.Journal.JournalDate>=from&&e.Journal.JournalDate<=to).Sum(e=>(decimal?)(e.Debit-e.Credit))??0}).ToListAsync(token);return [["Department","Cost centre","Account","Budget","Forecast","Actual","Variance"],..values.Select(x=>new[]{x.Department,x.CostCentre,x.Account,x.Amount.ToString("F2"),x.ForecastAmount.ToString("F2"),x.Actual.ToString("F2"),(x.Amount-x.Actual).ToString("F2")})]; }
        if (normalized is "driver-profitability") { var values=await db.Bookings.AsNoTracking().Where(x=>x.CompanyId==company.CompanyId&&x.DriverId!=null&&x.PickupDateTime>=new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero)&&x.PickupDateTime<=new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue),TimeSpan.Zero)).GroupBy(x=>new{x.DriverId,x.Driver!.FirstName,x.Driver.LastName}).Select(g=>new{Driver=g.Key.FirstName+" "+g.Key.LastName,Revenue=g.Sum(x=>x.TotalFare),Settlements=db.DriverSettlements.Where(s=>s.CompanyId==company.CompanyId&&s.DriverId==g.Key.DriverId&&s.PeriodEnd>=from&&s.PeriodStart<=to).Sum(s=>(decimal?)s.NetPayable)??0}).ToListAsync(token);return [["Driver","Revenue","Settlements","Contribution"],..values.Select(x=>new[]{x.Driver,x.Revenue.ToString("F2"),x.Settlements.ToString("F2"),(x.Revenue-x.Settlements).ToString("F2")})]; }
        if (normalized is "vehicle-profitability") { var values=await db.Bookings.AsNoTracking().Where(x=>x.CompanyId==company.CompanyId&&x.PickupDateTime>=new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero)&&x.PickupDateTime<=new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue),TimeSpan.Zero)).GroupBy(x=>x.VehicleType).Select(g=>new{Vehicle=g.Key,Bookings=g.Count(),Revenue=g.Sum(x=>x.TotalFare)}).ToListAsync(token);return [["Vehicle category","Bookings","Revenue"],..values.Select(x=>new[]{x.Vehicle.ToString(),x.Bookings.ToString(),x.Revenue.ToString("F2")})]; }
        if (normalized is "customer-profitability") { var values=await db.Bookings.AsNoTracking().Where(x=>x.CompanyId==company.CompanyId&&x.PickupDateTime>=new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero)&&x.PickupDateTime<=new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue),TimeSpan.Zero)).GroupBy(x=>new{x.CustomerId,x.Customer.FirstName,x.Customer.LastName}).Select(g=>new{Customer=g.Key.FirstName+" "+g.Key.LastName,Bookings=g.Count(),Revenue=g.Sum(x=>x.TotalFare)}).ToListAsync(token);return [["Customer","Bookings","Revenue"],..values.Select(x=>new[]{x.Customer,x.Bookings.ToString(),x.Revenue.ToString("F2")})]; }
        if (normalized is "corporate-profitability") { var values=await db.Bookings.AsNoTracking().Where(x=>x.CompanyId==company.CompanyId&&x.CorporateAccountId!=null&&x.PickupDateTime>=new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero)&&x.PickupDateTime<=new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue),TimeSpan.Zero)).GroupBy(x=>x.CorporateAccount!.AccountName).Select(g=>new{Corporate=g.Key,Bookings=g.Count(),Revenue=g.Sum(x=>x.TotalFare)}).ToListAsync(token);return [["Corporate account","Bookings","Revenue"],..values.Select(x=>new[]{x.Corporate,x.Bookings.ToString(),x.Revenue.ToString("F2")})]; }
        throw new InvalidOperationException("The requested finance report is unsupported.");
    }

    async Task<List<AccountBalance>> Balances(DateOnly at, CancellationToken token) => await db.LedgerAccounts.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.IsActive).Select(x => new AccountBalance(x.Id, x.Name, x.Type, x.OpeningBalance + db.JournalEntries.Where(e => e.CompanyId == company.CompanyId && e.LedgerAccountId == x.Id && e.Journal.Status == JournalStatus.Posted && e.Journal.JournalDate <= at).Sum(e => (decimal?)(e.Debit - e.Credit)) ?? 0)).ToListAsync(token);
    static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
    static byte[] Xlsx(IReadOnlyDictionary<string, IReadOnlyList<string[]>> sheets)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var overrides=string.Concat(sheets.Select((_,i)=>$"<Override PartName=\"/xl/worksheets/sheet{i+1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"));
            Add(archive, "[Content_Types].xml", $"<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>{overrides}<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/></Types>");
            Add(archive, "_rels/.rels", "<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            var relationships=string.Concat(sheets.Select((_,i)=>$"<Relationship Id=\"rId{i+1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i+1}.xml\"/>"));Add(archive,"xl/_rels/workbook.xml.rels",$"<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">{relationships}<Relationship Id=\"rId{sheets.Count+1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
            var workbookSheets=string.Concat(sheets.Select((x,i)=>$"<sheet name=\"{SecurityElement.Escape(x.Key)}\" sheetId=\"{i+1}\" r:id=\"rId{i+1}\"/>"));Add(archive,"xl/workbook.xml",$"<?xml version=\"1.0\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>{workbookSheets}</sheets></workbook>");
            Add(archive, "xl/styles.xml", "<?xml version=\"1.0\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><fonts count=\"2\"><font/><font><b/></font></fonts><fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills><borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf/></cellStyleXfs><cellXfs count=\"2\"><xf/><xf fontId=\"1\" applyFont=\"1\"/></cellXfs></styleSheet>");
            var sheetIndex=0;foreach(var item in sheets){sheetIndex++;var rows=item.Value;var lastColumn=Column(Math.Max(0,rows.FirstOrDefault()?.Length-1??0));var sheet=new StringBuilder($"<?xml version=\"1.0\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" state=\"frozen\"/></sheetView></sheetViews><autoFilter ref=\"A1:{lastColumn}1\"/><sheetData>");for(var row=0;row<rows.Count;row++){sheet.Append($"<row r=\"{row+1}\">");for(var column=0;column<rows[row].Length;column++)sheet.Append($"<c r=\"{Column(column)}{row+1}\" t=\"inlineStr\" s=\"{(row==0?1:0)}\"><is><t>{SecurityElement.Escape(rows[row][column])}</t></is></c>");sheet.Append("</row>");}sheet.Append("</sheetData><pageSetup orientation=\"landscape\" fitToWidth=\"1\"/></worksheet>");Add(archive,$"xl/worksheets/sheet{sheetIndex}.xml",sheet.ToString());}
        }
        return stream.ToArray();
    }
    static void Add(ZipArchive archive, string path, string content) { using var writer = new StreamWriter(archive.CreateEntry(path).Open(), new UTF8Encoding(false)); writer.Write(content); }
    static string Column(int index) { var value = string.Empty; do { value = (char)('A' + index % 26) + value; index = index / 26 - 1; } while (index >= 0); return value; }
    sealed record AccountBalance(Guid Id, string Name, LedgerAccountType Type, decimal Amount);
}

public sealed class CsvPayrollExportProvider : IPayrollExportProvider
{
    public string Key => "csv";
    public Task<FinanceExportResult> ExportAsync(IReadOnlyList<DriverSettlement> settlements, CancellationToken token = default)
    {
        var csv = new StringBuilder("Reference,DriverId,PeriodStart,PeriodEnd,GrossFares,Commission,Bonuses,Deductions,NetPayable\r\n");
        foreach (var x in settlements) csv.AppendLine($"{x.Reference},{x.DriverId},{x.PeriodStart:yyyy-MM-dd},{x.PeriodEnd:yyyy-MM-dd},{x.GrossFares:F2},{x.Commission:F2},{x.Bonuses:F2},{x.Penalties:F2},{x.NetPayable:F2}");
        return Task.FromResult(new FinanceExportResult("driver-payroll.csv", "text/csv", Encoding.UTF8.GetBytes(csv.ToString())));
    }
}
