namespace LondonVIP.Shared.Invoices;

public class InvoiceLineDto
{
    public Guid Id { get; set; }
    public Guid? BookingId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal LineSubtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
}

public class InvoiceListItemDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTimeOffset InvoiceDate { get; set; }
    public DateTimeOffset DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CustomerOrAccountName { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
}

public class InvoiceDetailDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTimeOffset InvoiceDate { get; set; }
    public DateTimeOffset DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? CorporateAccountId { get; set; }
    public string? CorporateAccountName { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<InvoiceLineDto> Lines { get; set; } = [];
}

public class InvoiceCreateDto
{
    public Guid? CorporateAccountId { get; set; }
    public Guid? CustomerId { get; set; }
    public DateTimeOffset? InvoiceDate { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public List<InvoiceLineCreateDto> Lines { get; set; } = [];
    public string? Notes { get; set; }
}

public class InvoiceLineCreateDto
{
    public Guid? BookingId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
}

public sealed class InvoiceUpdateDto : InvoiceCreateDto;

public class InvoiceSummaryDto
{
    public int DraftInvoices { get; set; }
    public int OutstandingInvoices { get; set; }
    public int OverdueInvoices { get; set; }
    public int PaidInvoices { get; set; }
    public decimal TotalOutstandingAmount { get; set; }
}

public class InvoiceStatusDto
{
    public Guid InvoiceId { get; set; }
    public string Status { get; set; } = string.Empty;
}
