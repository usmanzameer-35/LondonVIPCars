namespace LondonVIP.Shared.Customers;

public class CustomerCreateDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? SecondaryPhone { get; set; }
    public string? Address { get; set; }
    public string? Postcode { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}
