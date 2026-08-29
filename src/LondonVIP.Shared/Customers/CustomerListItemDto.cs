namespace LondonVIP.Shared.Customers;

public sealed class CustomerListItemDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Postcode { get; set; }
    public bool IsActive { get; set; }
    public int TotalBookings { get; set; }
    public DateTimeOffset? LastBookingDate { get; set; }
    public decimal TotalSpend { get; set; }
}
