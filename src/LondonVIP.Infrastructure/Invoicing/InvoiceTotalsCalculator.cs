using LondonVIP.Shared.Invoicing;
using LondonVIP.Shared.Models;

namespace LondonVIP.Infrastructure.Invoicing;

public sealed class InvoiceTotalsCalculator : IInvoiceTotalsCalculator
{
    public InvoiceTotals Calculate(Booking booking)
    {
        var subtotal = decimal.Round(booking.TotalFare, 2, MidpointRounding.AwayFromZero);
        return new InvoiceTotals(subtotal, 0m, subtotal);
    }
}
