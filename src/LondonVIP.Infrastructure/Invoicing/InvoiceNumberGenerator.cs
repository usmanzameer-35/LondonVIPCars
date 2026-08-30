using LondonVIP.Shared.Invoicing;

namespace LondonVIP.Infrastructure.Invoicing;

using LondonVIP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

/// <summary>Database-backed per-company invoice number generator.</summary>
public sealed class InvoiceNumberGenerator : IInvoiceNumberGenerator
{
    private readonly LondonVIPDbContext db;

    public InvoiceNumberGenerator(LondonVIPDbContext db) => this.db = db;

    public async Task<string> GenerateAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var sequence = await db.InvoiceNumberSequences.SingleOrDefaultAsync(x => x.CompanyId == companyId, cancellationToken);
        if (sequence is null)
        {
            sequence = new() { CompanyId = companyId, NextNumber = 2 };
            db.InvoiceNumberSequences.Add(sequence);
            await db.SaveChangesAsync(cancellationToken);
            return "LVC-000001";
        }

        var number = sequence.NextNumber;
        sequence.NextNumber++;
        await db.SaveChangesAsync(cancellationToken);
        return $"LVC-{number:000000}";
    }
}
