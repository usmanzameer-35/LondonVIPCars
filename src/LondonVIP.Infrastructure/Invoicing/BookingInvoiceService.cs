using LondonVIP.Infrastructure.Data;
using LondonVIP.Shared.Invoicing;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LondonVIP.Infrastructure.Invoicing;

public sealed class BookingInvoiceService(
    LondonVIPDbContext db,
    ICompanyContext companyContext,
    IInvoiceNumberGenerator numberGenerator,
    IInvoiceTotalsCalculator totalsCalculator) : IBookingInvoiceService
{
    public async Task<bool> CanGenerateInvoiceAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(
            b => b.Id == bookingId && b.CompanyId == companyContext.CompanyId, cancellationToken);
        return booking is not null && await IsInvoiceableAsync(booking, cancellationToken);
    }

    public async Task<BookingInvoiceResult> GenerateInvoiceAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await db.Bookings
            .Include(b => b.Customer)
            .Include(b => b.CorporateAccount)
            .SingleOrDefaultAsync(b => b.Id == bookingId && b.CompanyId == companyContext.CompanyId, cancellationToken);
        if (booking is null) return new(InvoiceGenerationOutcome.NotFound, Error: "Booking was not found.");

        var existing = await db.Invoices.Include(i => i.Lines)
            .SingleOrDefaultAsync(i => i.CompanyId == companyContext.CompanyId && i.Lines.Any(l => l.BookingId == bookingId), cancellationToken);
        if (existing is not null) return new(InvoiceGenerationOutcome.AlreadyExists, existing);
        if (!await IsInvoiceableAsync(booking, cancellationToken))
            return new(InvoiceGenerationOutcome.ValidationFailure, Error: "Booking is not eligible for invoicing.");

        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        existing = await db.Invoices.Include(i => i.Lines)
            .SingleOrDefaultAsync(i => i.CompanyId == companyContext.CompanyId && i.Lines.Any(l => l.BookingId == bookingId), cancellationToken);
        if (existing is not null) return new(InvoiceGenerationOutcome.AlreadyExists, existing);

        var totals = totalsCalculator.Calculate(booking);
        var now = DateTimeOffset.UtcNow;
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(), CompanyId = companyContext.CompanyId,
            CustomerId = booking.CustomerId, CorporateAccountId = booking.CorporateAccountId,
            InvoiceNumber = await numberGenerator.GenerateAsync(companyContext.CompanyId, cancellationToken),
            InvoiceDate = now, DueDate = now.AddDays(30), Status = InvoiceStatus.Issued,
            Subtotal = totals.Subtotal, TaxAmount = totals.TaxAmount, TotalAmount = totals.TotalAmount,
            AmountPaid = 0m, BalanceDue = totals.TotalAmount, CreatedAt = now, UpdatedAt = now
        };
        invoice.Lines.Add(new InvoiceLine
        {
            Id = Guid.NewGuid(), BookingId = booking.Id, Description = $"Booking {booking.BookingReference}",
            Quantity = 1m, UnitPrice = totals.Subtotal, TaxRate = 0m, LineSubtotal = totals.Subtotal,
            TaxAmount = totals.TaxAmount, LineTotal = totals.TotalAmount, CreatedAt = now
        });
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(InvoiceGenerationOutcome.Success, invoice);
    }

    private async Task<bool> IsInvoiceableAsync(Booking booking, CancellationToken cancellationToken)
    {
        if (booking.Status != BookingStatus.Completed || booking.TotalFare <= 0m ||
            booking.BookingReference.Contains("TEST", StringComparison.OrdinalIgnoreCase)) return false;
        return !await db.InvoiceLines.AnyAsync(l => l.BookingId == booking.Id && l.Invoice.CompanyId == companyContext.CompanyId, cancellationToken);
    }
}
