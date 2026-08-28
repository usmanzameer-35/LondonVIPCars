using LondonVIP.Infrastructure.Data;
using LondonVIP.Shared.CompanySetup;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;
using LondonVIP.Infrastructure.Security;
using LondonVIP.Shared.Security;

namespace LondonVIP.Api;

public static class CompanySetupEndpoints
{
    public static IEndpointRouteBuilder MapCompanySetupEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/company/setup").RequireAuthorization(SecurityPolicies.CompanyAdministration).RequireRateLimiting("operations");
        group.MapGet("", GetSetupAsync);
        group.MapPut("", UpdateSetupAsync);
        return endpoints;
    }

    private static async Task<IResult> GetSetupAsync(
        LondonVIPDbContext dbContext,
        ICompanyContext companyContext,
        CancellationToken cancellationToken)
    {
        var company = await LoadCompanyAsync(dbContext, companyContext.CompanyId, cancellationToken);
        return company is null ? Results.NotFound() : Results.Ok(ToDto(company));
    }

    private static async Task<IResult> UpdateSetupAsync(
        CompanySetupDto setup,
        LondonVIPDbContext dbContext,
        ICompanyContext companyContext,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var errors = CompanySetupValidator.Validate(setup);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var company = await LoadCompanyAsync(dbContext, companyContext.CompanyId, cancellationToken);
        if (company is null) return Results.NotFound();

        Apply(setup, company);
        await dbContext.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("CompanySetupUpdated", "Company", "Succeeded", SecurityEventSeverity.Warning, "Company setup was updated.", "Company", company.Id.ToString(), company.Id, cancellationToken);
        return Results.Ok(ToDto(company));
    }

    private static Task<Company?> LoadCompanyAsync(LondonVIPDbContext dbContext, Guid companyId, CancellationToken cancellationToken) =>
        dbContext.Companies
            .Include(company => company.Settings)
            .Include(company => company.Branding)
            .SingleOrDefaultAsync(company => company.Id == companyId, cancellationToken);

    private static CompanySetupDto ToDto(Company company)
    {
        var settings = company.Settings ?? new CompanySettings { CompanyId = company.Id };
        var branding = company.Branding ?? new CompanyBranding { CompanyId = company.Id };
        return new CompanySetupDto
        {
            Profile = new CompanyProfileDto
            {
                TradingName = company.TradingName,
                LegalName = company.LegalName,
                Email = company.Email,
                Phone = company.Phone,
                Website = company.WebsiteUrl,
                Address = company.AddressLine1,
                City = company.City,
                Postcode = company.Postcode,
                Country = company.Country,
                TimeZone = company.TimeZone,
                Currency = company.CurrencyCode
            },
            Branding = new CompanyBrandingDto
            {
                PrimaryColour = branding.PrimaryColour,
                SecondaryColour = branding.SecondaryColour,
                AccentColour = branding.AccentColour,
                LogoUrl = branding.LogoUrl,
                FaviconUrl = branding.FaviconUrl
            },
            Operations = new CompanyOperationsDto
            {
                MinimumBookingNoticeMinutes = settings.MinimumBookingNoticeMinutes,
                FreeAirportWaitingMinutes = settings.FreeAirportWaitingMinutes,
                WaitingChargePerHour = settings.WaitingChargePerHour,
                DefaultAirportPickupSupplement = settings.DefaultAirportPickupSupplement,
                MeetAndGreetEnabled = settings.MeetAndGreetEnabled,
                DriverCommissionPercentage = settings.DriverCommissionPercentage,
                DriverWeeklySubscriptionAmount = settings.DriverWeeklySubscriptionAmount,
                DefaultLanguage = settings.DefaultLanguage
            },
            Invoice = new CompanyInvoiceSettingsDto
            {
                VatEnabled = settings.VatEnabled,
                VatRate = settings.VatRate,
                InvoicePrefix = settings.InvoicePrefix
            },
            Website = new CompanyWebsiteSettingsDto
            {
                WebsiteTitle = branding.CustomerWebsiteTitle,
                WebsiteTagline = branding.CustomerWebsiteTagline
            }
        };
    }

    private static void Apply(CompanySetupDto setup, Company company)
    {
        company.Settings ??= new CompanySettings { CompanyId = company.Id };
        company.Branding ??= new CompanyBranding { CompanyId = company.Id };

        company.TradingName = setup.Profile.TradingName.Trim();
        company.LegalName = setup.Profile.LegalName.Trim();
        company.Email = setup.Profile.Email.Trim();
        company.Phone = setup.Profile.Phone.Trim();
        company.WebsiteUrl = setup.Profile.Website.Trim();
        company.AddressLine1 = setup.Profile.Address.Trim();
        company.City = setup.Profile.City.Trim();
        company.Postcode = setup.Profile.Postcode.Trim();
        company.Country = setup.Profile.Country.Trim();
        company.TimeZone = setup.Profile.TimeZone.Trim();
        company.CurrencyCode = setup.Profile.Currency.Trim().ToUpperInvariant();
        company.UpdatedAt = DateTimeOffset.UtcNow;

        company.Branding.PrimaryColour = setup.Branding.PrimaryColour.Trim();
        company.Branding.SecondaryColour = setup.Branding.SecondaryColour.Trim();
        company.Branding.AccentColour = setup.Branding.AccentColour.Trim();
        company.Branding.LogoUrl = setup.Branding.LogoUrl.Trim();
        company.Branding.FaviconUrl = setup.Branding.FaviconUrl.Trim();
        company.Branding.CustomerWebsiteTitle = setup.Website.WebsiteTitle.Trim();
        company.Branding.CustomerWebsiteTagline = setup.Website.WebsiteTagline.Trim();

        company.Settings.MinimumBookingNoticeMinutes = setup.Operations.MinimumBookingNoticeMinutes;
        company.Settings.FreeAirportWaitingMinutes = setup.Operations.FreeAirportWaitingMinutes;
        company.Settings.WaitingChargePerHour = setup.Operations.WaitingChargePerHour;
        company.Settings.DefaultAirportPickupSupplement = setup.Operations.DefaultAirportPickupSupplement;
        company.Settings.MeetAndGreetEnabled = setup.Operations.MeetAndGreetEnabled;
        company.Settings.DriverCommissionPercentage = setup.Operations.DriverCommissionPercentage;
        company.Settings.DriverWeeklySubscriptionAmount = setup.Operations.DriverWeeklySubscriptionAmount;
        company.Settings.DefaultLanguage = setup.Operations.DefaultLanguage.Trim();
        company.Settings.VatEnabled = setup.Invoice.VatEnabled;
        company.Settings.VatRate = setup.Invoice.VatRate;
        company.Settings.InvoicePrefix = setup.Invoice.InvoicePrefix.Trim().ToUpperInvariant();
    }
}
