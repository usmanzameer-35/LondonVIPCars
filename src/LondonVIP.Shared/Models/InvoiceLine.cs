namespace LondonVIP.Shared.Models;

/// <summary>
/// Represents a line item on an invoice.
/// </summary>
public class InvoiceLine
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public Guid? BookingId { get; set; }
    public Booking? Booking { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal LineSubtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
