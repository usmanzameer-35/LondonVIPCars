namespace LondonVIP.Shared.CompanySetup;

public sealed class CompanyProfileDto
{
    public string TradingName { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
}
