using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Security;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Pricing;
using LondonVIP.Shared.Security;
using LondonVIP.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LondonVIP.Api;

public static class PricingAdministrationEndpoints
{
    public static IEndpointRouteBuilder MapPricingAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/pricing").RequireRateLimiting("operations");
        group.MapGet("", GetRulesAsync).RequireAuthorization(SecurityPolicies.PricingRead);
        group.MapGet("/summary", GetSummaryAsync).RequireAuthorization(SecurityPolicies.PricingRead);
        group.MapGet("/airports", GetAirportsAsync).RequireAuthorization(SecurityPolicies.PricingRead);
        group.MapGet("/{id:guid}", GetRuleAsync).RequireAuthorization(SecurityPolicies.PricingRead);
        group.MapPost("", CreateRuleAsync).RequireAuthorization(SecurityPolicies.PricingWrite);
        group.MapPut("/{id:guid}", UpdateRuleAsync).RequireAuthorization(SecurityPolicies.PricingWrite);
        group.MapPatch("/{id:guid}/status", SetStatusAsync).RequireAuthorization(SecurityPolicies.PricingWrite);
        return endpoints;
    }

    private static async Task<IResult> GetRulesAsync(LondonVIPDbContext db, ICompanyContext company, CancellationToken cancellationToken)
    {
        var rules = await RuleQuery(db, company.CompanyId).ToListAsync(cancellationToken);
        return Results.Ok(rules.OrderBy(item => item.AirportName ?? string.Empty).ThenBy(item => item.VehicleType));
    }

    private static async Task<IResult> GetRuleAsync(Guid id, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, CancellationToken cancellationToken)
    {
        var rule = await DetailQuery(db, company.CompanyId).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (rule is null) { await AuditCrossTenantAsync(db, audit, id, company.CompanyId, cancellationToken); return Results.NotFound(); }
        return Results.Ok(rule);
    }

    private static async Task<IResult> GetAirportsAsync(LondonVIPDbContext db, CancellationToken cancellationToken) =>
        Results.Ok(await db.Airports.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Name)
            .Select(item => new PricingAirportLookupDto(item.Id, item.Code, item.Name)).ToListAsync(cancellationToken));

    private static async Task<IResult> GetSummaryAsync(LondonVIPDbContext db, ICompanyContext company, CancellationToken cancellationToken)
    {
        var rules = await db.PricingRules.AsNoTracking().Where(item => item.CompanyId == company.CompanyId)
            .Select(item => new { item.AirportId, item.VehicleType, item.IsActive }).ToListAsync(cancellationToken);
        var settings = await db.CompanySettings.AsNoTracking().SingleOrDefaultAsync(item => item.CompanyId == company.CompanyId, cancellationToken);
        return Results.Ok(new PricingSummaryDto
        {
            ActiveRules = rules.Count(item => item.IsActive), InactiveRules = rules.Count(item => !item.IsActive),
            AirportsConfigured = rules.Where(item => item.IsActive && item.AirportId.HasValue).Select(item => item.AirportId).Distinct().Count(),
            VehicleTypesConfigured = rules.Where(item => item.IsActive).Select(item => item.VehicleType).Distinct().Count(),
            DefaultAirportPickupSupplement = settings?.DefaultAirportPickupSupplement ?? 0,
            DefaultFreeAirportWaitingMinutes = settings?.FreeAirportWaitingMinutes ?? 0,
            DefaultWaitingChargePerHour = settings?.WaitingChargePerHour ?? 0,
            MeetAndGreetEnabled = settings?.MeetAndGreetEnabled ?? false,
            MinimumBookingNoticeMinutes = settings?.MinimumBookingNoticeMinutes ?? 0
        });
    }

    private static async Task<IResult> CreateRuleAsync(PricingRuleCreateDto request, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, CancellationToken cancellationToken)
    {
        var errors = PricingRuleValidator.Validate(request);
        await AddReferenceErrorsAsync(errors, request, null, db, company.CompanyId, cancellationToken);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var now = DateTimeOffset.UtcNow;
        var rule = new PricingRule { Id = Guid.NewGuid(), CompanyId = company.CompanyId, CreatedAt = now, UpdatedAt = now };
        Apply(request, rule); db.PricingRules.Add(rule); await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("PricingRuleCreated", "Pricing", "Succeeded", SecurityEventSeverity.Information, "Pricing rule created.", "PricingRule", rule.Id.ToString(), company.CompanyId, cancellationToken);
        return Results.Created($"/api/pricing/{rule.Id}", await DetailQuery(db, company.CompanyId).SingleAsync(item => item.Id == rule.Id, cancellationToken));
    }

    private static async Task<IResult> UpdateRuleAsync(Guid id, PricingRuleUpdateDto request, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, CancellationToken cancellationToken)
    {
        var errors = PricingRuleValidator.Validate(request);
        await AddReferenceErrorsAsync(errors, request, id, db, company.CompanyId, cancellationToken);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var rule = await db.PricingRules.SingleOrDefaultAsync(item => item.Id == id && item.CompanyId == company.CompanyId, cancellationToken);
        if (rule is null) { await AuditCrossTenantAsync(db, audit, id, company.CompanyId, cancellationToken); return Results.NotFound(); }
        Apply(request, rule); rule.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("PricingRuleUpdated", "Pricing", "Succeeded", SecurityEventSeverity.Information, "Pricing rule updated.", "PricingRule", id.ToString(), company.CompanyId, cancellationToken);
        return Results.Ok(await DetailQuery(db, company.CompanyId).SingleAsync(item => item.Id == id, cancellationToken));
    }

    private static async Task<IResult> SetStatusAsync(Guid id, PricingRuleStatusDto request, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, CancellationToken cancellationToken)
    {
        var rule = await db.PricingRules.SingleOrDefaultAsync(item => item.Id == id && item.CompanyId == company.CompanyId, cancellationToken);
        if (rule is null) { await AuditCrossTenantAsync(db, audit, id, company.CompanyId, cancellationToken); return Results.NotFound(); }
        if (request.IsActive && !rule.IsActive && await HasActiveDuplicateAsync(db, company.CompanyId, rule.AirportId, rule.VehicleType, id, cancellationToken))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["isActive"] = ["Another active rule already exists for this airport and vehicle type."] });
        if (rule.IsActive == request.IsActive) return Results.Ok(await DetailQuery(db, company.CompanyId).SingleAsync(item => item.Id == id, cancellationToken));
        rule.IsActive = request.IsActive; rule.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(request.IsActive ? "PricingRuleActivated" : "PricingRuleDeactivated", "Pricing", "Succeeded", SecurityEventSeverity.Information,
            request.IsActive ? "Pricing rule activated." : "Pricing rule deactivated.", "PricingRule", id.ToString(), company.CompanyId, cancellationToken);
        return Results.Ok(await DetailQuery(db, company.CompanyId).SingleAsync(item => item.Id == id, cancellationToken));
    }

    private static async Task AddReferenceErrorsAsync(Dictionary<string, string[]> errors, PricingRuleCreateDto request, Guid? excludingId, LondonVIPDbContext db, Guid companyId, CancellationToken cancellationToken)
    {
        if (request.AirportId is { } airportId && !await db.Airports.AnyAsync(item => item.Id == airportId && item.IsActive, cancellationToken)) errors["airportId"] = ["Airport was not found or is inactive."];
        if (request.IsActive && await HasActiveDuplicateAsync(db, companyId, request.AirportId, request.VehicleType, excludingId, cancellationToken)) errors["rule"] = ["An active rule already exists for this airport and vehicle type."];
    }

    private static Task<bool> HasActiveDuplicateAsync(LondonVIPDbContext db, Guid companyId, Guid? airportId, VehicleType vehicleType, Guid? excludingId, CancellationToken cancellationToken) =>
        db.PricingRules.AnyAsync(item => item.CompanyId == companyId && item.Id != excludingId && item.AirportId == airportId && item.VehicleType == vehicleType && item.IsActive, cancellationToken);

    private static void Apply(PricingRuleCreateDto request, PricingRule rule)
    {
        rule.AirportId = request.AirportId; rule.VehicleType = request.VehicleType; rule.BasePrice = request.BasePrice;
        rule.AirportPickupSupplement = request.AirportPickupSupplement; rule.FreeWaitingMinutes = request.FreeWaitingMinutes;
        rule.WaitingChargePerHour = request.WaitingChargePerHour; rule.IsActive = request.IsActive;
    }

    private static IQueryable<PricingRuleListItemDto> RuleQuery(LondonVIPDbContext db, Guid companyId) =>
        db.PricingRules.AsNoTracking().Where(item => item.CompanyId == companyId).Select(item => new PricingRuleListItemDto
        {
            Id = item.Id, AirportId = item.AirportId, AirportCode = item.AirportId == null ? null : db.Airports.Where(airport => airport.Id == item.AirportId).Select(airport => airport.Code).FirstOrDefault(),
            AirportName = item.AirportId == null ? null : db.Airports.Where(airport => airport.Id == item.AirportId).Select(airport => airport.Name).FirstOrDefault(),
            VehicleType = item.VehicleType, BasePrice = item.BasePrice, AirportPickupSupplement = item.AirportPickupSupplement,
            FreeWaitingMinutes = item.FreeWaitingMinutes, WaitingChargePerHour = item.WaitingChargePerHour, IsActive = item.IsActive, UpdatedAt = item.UpdatedAt
        });

    private static IQueryable<PricingRuleDetailDto> DetailQuery(LondonVIPDbContext db, Guid companyId) =>
        db.PricingRules.AsNoTracking().Where(item => item.CompanyId == companyId).Select(item => new PricingRuleDetailDto
        {
            Id = item.Id, AirportId = item.AirportId, AirportCode = item.AirportId == null ? null : db.Airports.Where(airport => airport.Id == item.AirportId).Select(airport => airport.Code).FirstOrDefault(),
            AirportName = item.AirportId == null ? null : db.Airports.Where(airport => airport.Id == item.AirportId).Select(airport => airport.Name).FirstOrDefault(),
            VehicleType = item.VehicleType, BasePrice = item.BasePrice, AirportPickupSupplement = item.AirportPickupSupplement,
            FreeWaitingMinutes = item.FreeWaitingMinutes, WaitingChargePerHour = item.WaitingChargePerHour, IsActive = item.IsActive,
            CreatedAt = item.CreatedAt, UpdatedAt = item.UpdatedAt
        });

    private static async Task AuditCrossTenantAsync(LondonVIPDbContext db, IAuditService audit, Guid id, Guid companyId, CancellationToken cancellationToken)
    {
        if (await db.PricingRules.AnyAsync(item => item.Id == id && item.CompanyId != companyId, cancellationToken))
            await audit.WriteAsync("CrossTenantAccessAttempt", "Authorization", "Denied", SecurityEventSeverity.High, "Cross-tenant pricing rule access was blocked.", "PricingRule", id.ToString(), companyId, cancellationToken);
    }
}
