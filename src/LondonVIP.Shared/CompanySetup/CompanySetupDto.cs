namespace LondonVIP.Shared.CompanySetup;

public sealed class CompanySetupDto
{
    public CompanyProfileDto Profile { get; set; } = new();
    public CompanyBrandingDto Branding { get; set; } = new();
    public CompanyOperationsDto Operations { get; set; } = new();
    public CompanyInvoiceSettingsDto Invoice { get; set; } = new();
    public CompanyWebsiteSettingsDto Website { get; set; } = new();
}
