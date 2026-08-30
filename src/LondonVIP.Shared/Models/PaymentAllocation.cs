namespace LondonVIP.Shared.Models;

/// <summary>
/// Represents an allocation of a payment to an invoice. Many-to-many linking.
/// </summary>
public class PaymentAllocation
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
