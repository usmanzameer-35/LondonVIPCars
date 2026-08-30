namespace LondonVIP.Shared.Payments;

public class PaymentAllocationDto
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class PaymentListItemDto
{
    public Guid Id { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
    public DateTimeOffset PaymentDate { get; set; }
    public string? CustomerOrAccountName { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal AllocatedAmount { get; set; }
    public decimal UnallocatedAmount { get; set; }
}

public class PaymentDetailDto
{
    public Guid Id { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
    public DateTimeOffset PaymentDate { get; set; }
    public Guid? CorporateAccountId { get; set; }
    public string? CorporateAccountName { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<PaymentAllocationDto> Allocations { get; set; } = [];
}

public class PaymentCreateDto
{
    public string PaymentReference { get; set; } = string.Empty;
    public DateTimeOffset? PaymentDate { get; set; }
    public Guid? CorporateAccountId { get; set; }
    public Guid? CustomerId { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

public sealed class PaymentUpdateDto : PaymentCreateDto;

public class PaymentAllocationCreateDto
{
    public Guid InvoiceId { get; set; }
    public decimal Amount { get; set; }
}

public class PaymentSummaryDto
{
    public decimal PaymentsReceived { get; set; }
    public decimal UnallocatedAmount { get; set; }
    public decimal AllocatedAmount { get; set; }
    public int PaymentCount { get; set; }
}
