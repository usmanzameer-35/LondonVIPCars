namespace LondonVIP.Shared.Models;

/// <summary>Per-company counter used for allocating invoice numbers.</summary>
public class InvoiceNumberSequence
{
    public Guid CompanyId { get; set; }
    public long NextNumber { get; set; } = 1;
    public Company Company { get; set; } = null!;
}
