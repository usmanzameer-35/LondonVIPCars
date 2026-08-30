namespace LondonVIP.Shared.Models;

public class Company
{
    public Guid Id { get; set; }
    public string TradingName { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string WebsiteUrl { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string AddressLine2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public CompanySettings? Settings { get; set; }
    public CompanyBranding? Branding { get; set; }
    public ICollection<Customer> Customers { get; set; } = [];
    public ICollection<Driver> Drivers { get; set; } = [];
    public ICollection<Vehicle> Vehicles { get; set; } = [];
    public ICollection<Booking> Bookings { get; set; } = [];
    public ICollection<PricingRule> PricingRules { get; set; } = [];
    public ICollection<Invoice> Invoices { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<Quotation> Quotations { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
}
