using System.Globalization;
using System.Text.RegularExpressions;

namespace LondonVIP.Shared.CompanySetup;

public static partial class CompanySetupValidator
{
    public static Dictionary<string, string[]> Validate(CompanySetupDto? setup)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var messages)) errors[field] = messages = [];
            messages.Add(message);
        }
        void Required(string field, string? value, string label)
        {
            if (string.IsNullOrWhiteSpace(value)) Add(field, $"{label} is required.");
        }
        void Range(string field, decimal value, decimal minimum, decimal maximum, string label)
        {
            if (value < minimum || value > maximum) Add(field, $"{label} must be between {minimum.ToString(CultureInfo.InvariantCulture)} and {maximum.ToString(CultureInfo.InvariantCulture)}.");
        }

        if (setup is null)
        {
            Add(string.Empty, "Company setup data is required.");
            return errors.ToDictionary(item => item.Key, item => item.Value.ToArray());
        }

        if (setup.Profile is null || setup.Branding is null || setup.Operations is null || setup.Invoice is null || setup.Website is null)
        {
            Add(string.Empty, "All company setup sections are required.");
            return errors.ToDictionary(item => item.Key, item => item.Value.ToArray());
        }

        Required("profile.tradingName", setup.Profile.TradingName, "Trading name");
        Required("profile.legalName", setup.Profile.LegalName, "Legal name");
        Required("profile.email", setup.Profile.Email, "Email");
        Required("profile.phone", setup.Profile.Phone, "Phone");
        Required("profile.address", setup.Profile.Address, "Address");
        Required("profile.city", setup.Profile.City, "City");
        Required("profile.postcode", setup.Profile.Postcode, "Postcode");
        Required("profile.country", setup.Profile.Country, "Country");
        Required("profile.timeZone", setup.Profile.TimeZone, "Time zone");
        Required("profile.currency", setup.Profile.Currency, "Currency");
        Required("operations.defaultLanguage", setup.Operations.DefaultLanguage, "Default language");
        Required("invoice.invoicePrefix", setup.Invoice.InvoicePrefix, "Invoice prefix");
        Required("website.websiteTitle", setup.Website.WebsiteTitle, "Website title");

        if (!string.IsNullOrWhiteSpace(setup.Profile.Email) && !EmailPattern().IsMatch(setup.Profile.Email)) Add("profile.email", "Email must be a valid address.");
        if (!string.IsNullOrWhiteSpace(setup.Profile.Currency) && !CurrencyPattern().IsMatch(setup.Profile.Currency)) Add("profile.currency", "Currency must be a three-letter uppercase code.");
        foreach (var colour in new[] { ("branding.primaryColour", setup.Branding.PrimaryColour), ("branding.secondaryColour", setup.Branding.SecondaryColour), ("branding.accentColour", setup.Branding.AccentColour) })
            if (!HexColourPattern().IsMatch(colour.Item2 ?? string.Empty)) Add(colour.Item1, "Colour must use #RRGGBB format.");

        foreach (var url in new[] { ("profile.website", setup.Profile.Website), ("branding.logoUrl", setup.Branding.LogoUrl), ("branding.faviconUrl", setup.Branding.FaviconUrl) })
            if (!string.IsNullOrWhiteSpace(url.Item2) && (!Uri.TryCreate(url.Item2, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))) Add(url.Item1, "URL must be an absolute HTTP or HTTPS address.");

        Range("operations.minimumBookingNoticeMinutes", setup.Operations.MinimumBookingNoticeMinutes, 0, 10080, "Minimum booking notice");
        Range("operations.freeAirportWaitingMinutes", setup.Operations.FreeAirportWaitingMinutes, 0, 1440, "Free airport waiting minutes");
        Range("operations.waitingChargePerHour", setup.Operations.WaitingChargePerHour, 0, 10000, "Waiting charge per hour");
        Range("operations.defaultAirportPickupSupplement", setup.Operations.DefaultAirportPickupSupplement, 0, 10000, "Airport pickup supplement");
        Range("operations.driverCommissionPercentage", setup.Operations.DriverCommissionPercentage, 0, 100, "Driver commission percentage");
        Range("operations.driverWeeklySubscriptionAmount", setup.Operations.DriverWeeklySubscriptionAmount, 0, 10000, "Driver weekly subscription amount");
        Range("invoice.vatRate", setup.Invoice.VatRate, 0, 100, "VAT rate");
        if (!setup.Invoice.VatEnabled && setup.Invoice.VatRate != 0) Add("invoice.vatRate", "VAT rate must be zero when VAT is disabled.");
        if (setup.Invoice.InvoicePrefix.Length > 20) Add("invoice.invoicePrefix", "Invoice prefix cannot exceed 20 characters.");

        return errors.ToDictionary(item => item.Key, item => item.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailPattern();
    [GeneratedRegex(@"^[A-Z]{3}$")]
    private static partial Regex CurrencyPattern();
    [GeneratedRegex(@"^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColourPattern();
}
