using System.Text;
using System.IO.Compression;
using System.Security;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Shared.Accounting;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LondonVIP.Infrastructure.Accounting;

public sealed class FiscalPeriodService(LondonVIPDbContext db, ICompanyContext company) : IFiscalPeriodService
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
        if (year.Periods.Any(x => x.Status != AccountingPeriodStatus.Closed)) throw new InvalidOperationException("All accounting periods must be closed first.");
        year.IsClosed = true;
        year.ClosedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(token);
        return true;
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
        var rows = await db.JournalEntries.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.Journal.Status == JournalStatus.Posted && x.Journal.JournalDate >= from && x.Journal.JournalDate <= to)
            .OrderBy(x => x.Journal.JournalDate).Select(x => new { x.Journal.JournalDate, x.Journal.Reference, Account = x.LedgerAccount.Code, x.Description, x.Debit, x.Credit }).ToListAsync(token);
        var csv = new StringBuilder("Date,Reference,Account,Description,Debit,Credit\r\n");
        foreach (var row in rows) csv.AppendLine($"{row.JournalDate:yyyy-MM-dd},{Escape(row.Reference)},{Escape(row.Account)},{Escape(row.Description)},{row.Debit:F2},{row.Credit:F2}");
        if (format.Equals("excel", StringComparison.OrdinalIgnoreCase) || format.Equals("xlsx", StringComparison.OrdinalIgnoreCase))
        {
            var data = new List<string[]> { new[] { "Date", "Reference", "Account", "Description", "Debit", "Credit" } };
            data.AddRange(rows.Select(x => new[] { x.JournalDate.ToString("yyyy-MM-dd"), x.Reference, x.Account, x.Description, x.Debit.ToString("F2"), x.Credit.ToString("F2") }));
            return new($"{report}-{from:yyyyMMdd}-{to:yyyyMMdd}.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", Xlsx(data, "General Ledger"));
        }
        return new($"{report}-{from:yyyyMMdd}-{to:yyyyMMdd}.csv", "text/csv", Encoding.UTF8.GetBytes(csv.ToString()));
    }

    async Task<List<AccountBalance>> Balances(DateOnly at, CancellationToken token) => await db.LedgerAccounts.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.IsActive).Select(x => new AccountBalance(x.Id, x.Name, x.Type, x.OpeningBalance + db.JournalEntries.Where(e => e.CompanyId == company.CompanyId && e.LedgerAccountId == x.Id && e.Journal.Status == JournalStatus.Posted && e.Journal.JournalDate <= at).Sum(e => (decimal?)(e.Debit - e.Credit)) ?? 0)).ToListAsync(token);
    static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
    static byte[] Xlsx(IReadOnlyList<string[]> rows, string sheetName)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            Add(archive, "[Content_Types].xml", "<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/></Types>");
            Add(archive, "_rels/.rels", "<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Add(archive, "xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
            Add(archive, "xl/workbook.xml", $"<?xml version=\"1.0\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"{SecurityElement.Escape(sheetName)}\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Add(archive, "xl/styles.xml", "<?xml version=\"1.0\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><fonts count=\"2\"><font/><font><b/></font></fonts><fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills><borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf/></cellStyleXfs><cellXfs count=\"2\"><xf/><xf fontId=\"1\" applyFont=\"1\"/></cellXfs></styleSheet>");
            var sheet = new StringBuilder("<?xml version=\"1.0\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" state=\"frozen\"/></sheetView></sheetViews><autoFilter ref=\"A1:F1\"/><sheetData>");
            for (var row = 0; row < rows.Count; row++) { sheet.Append($"<row r=\"{row + 1}\">"); for (var column = 0; column < rows[row].Length; column++) sheet.Append($"<c r=\"{Column(column)}{row + 1}\" t=\"inlineStr\" s=\"{(row == 0 ? 1 : 0)}\"><is><t>{SecurityElement.Escape(rows[row][column])}</t></is></c>"); sheet.Append("</row>"); }
            sheet.Append("</sheetData><pageSetup orientation=\"landscape\" fitToWidth=\"1\"/></worksheet>"); Add(archive, "xl/worksheets/sheet1.xml", sheet.ToString());
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
