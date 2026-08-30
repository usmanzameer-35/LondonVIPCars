using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.Invoicing;

public enum InvoiceGenerationOutcome { Success, AlreadyExists, ValidationFailure, NotFound }

public sealed record BookingInvoiceResult(InvoiceGenerationOutcome Outcome, Invoice? Invoice = null, string? Error = null)
{
    public bool Success => Outcome == InvoiceGenerationOutcome.Success;
}

public interface IBookingInvoiceService
{
    Task<bool> CanGenerateInvoiceAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task<BookingInvoiceResult> GenerateInvoiceAsync(Guid bookingId, CancellationToken cancellationToken = default);
}

public interface IInvoiceNumberGenerator
{
    Task<string> GenerateAsync(Guid companyId, CancellationToken cancellationToken = default);
}

public sealed record InvoiceTotals(decimal Subtotal, decimal TaxAmount, decimal TotalAmount);

public interface IInvoiceTotalsCalculator
{
    InvoiceTotals Calculate(Booking booking);
}
