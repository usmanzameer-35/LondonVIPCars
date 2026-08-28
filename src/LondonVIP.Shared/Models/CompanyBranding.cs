namespace LondonVIP.Shared.Models;

public class CompanyBranding
{
    public Guid CompanyId { get; set; }
    public string PrimaryColour { get; set; } = string.Empty;
    public string SecondaryColour { get; set; } = string.Empty;
    public string AccentColour { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
    public string FaviconUrl { get; set; } = string.Empty;
    public string CustomerWebsiteTitle { get; set; } = string.Empty;
    public string CustomerWebsiteTagline { get; set; } = string.Empty;

    public Company Company { get; set; } = null!;
}
