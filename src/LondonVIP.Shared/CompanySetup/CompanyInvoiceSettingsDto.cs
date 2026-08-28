namespace LondonVIP.Shared.CompanySetup;

public sealed class CompanyInvoiceSettingsDto
{
    public bool VatEnabled { get; set; }
    public decimal VatRate { get; set; }
    public string InvoicePrefix { get; set; } = string.Empty;
}
