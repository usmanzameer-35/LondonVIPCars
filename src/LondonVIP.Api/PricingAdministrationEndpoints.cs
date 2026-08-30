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
        group.MapPost("/{id:guid}/clone", CloneRuleAsync).RequireAuthorization(SecurityPolicies.PricingWrite);
        group.MapPost("/preview", PreviewAsync).RequireAuthorization(SecurityPolicies.PricingRead);
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
        if (request.IsActive && !rule.IsActive && rule.RuleType == PricingRuleType.LegacyFare && await HasLegacyDuplicateAsync(db, company.CompanyId, rule.AirportId, rule.VehicleType, id, cancellationToken))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["isActive"] = ["Another active legacy rule already exists for this airport and vehicle type."] });
        Apply(request, rule); rule.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("PricingRuleUpdated", "Pricing", "Succeeded", SecurityEventSeverity.Information, "Pricing rule updated.", "PricingRule", id.ToString(), company.CompanyId, cancellationToken);
        return Results.Ok(await DetailQuery(db, company.CompanyId).SingleAsync(item => item.Id == id, cancellationToken));
    }

    private static async Task<IResult> SetStatusAsync(Guid id, PricingRuleStatusDto request, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, CancellationToken cancellationToken)
    {
        var rule = await db.PricingRules.SingleOrDefaultAsync(item => item.Id == id && item.CompanyId == company.CompanyId, cancellationToken);
        if (rule is null) { await AuditCrossTenantAsync(db, audit, id, company.CompanyId, cancellationToken); return Results.NotFound(); }
        if (rule.IsActive == request.IsActive) return Results.Ok(await DetailQuery(db, company.CompanyId).SingleAsync(item => item.Id == id, cancellationToken));
        rule.IsActive = request.IsActive; rule.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(request.IsActive ? "PricingRuleActivated" : "PricingRuleDeactivated", "Pricing", "Succeeded", SecurityEventSeverity.Information,
            request.IsActive ? "Pricing rule activated." : "Pricing rule deactivated.", "PricingRule", id.ToString(), company.CompanyId, cancellationToken);
        return Results.Ok(await DetailQuery(db, company.CompanyId).SingleAsync(item => item.Id == id, cancellationToken));
    }

    private static async Task AddReferenceErrorsAsync(Dictionary<string, string[]> errors, PricingRuleCreateDto request, Guid? excludingId, LondonVIPDbContext db, Guid companyId, CancellationToken cancellationToken)
    {
        if (request.AirportId is { } airportId && !await db.Airports.AnyAsync(item => item.Id == airportId && item.IsActive, cancellationToken)) errors["airportId"] = ["Airport was not found or is inactive."];
        if (request.IsActive && request.RuleType == PricingRuleType.LegacyFare && await HasLegacyDuplicateAsync(db, companyId, request.AirportId, request.VehicleType, excludingId, cancellationToken)) errors["rule"] = ["An active rule already exists for this airport and vehicle type."];
    }

    private static Task<bool> HasLegacyDuplicateAsync(LondonVIPDbContext db, Guid companyId, Guid? airportId, VehicleType vehicleType, Guid? excludingId, CancellationToken token) =>
        db.PricingRules.AnyAsync(item => item.CompanyId == companyId && item.Id != excludingId && item.RuleType == PricingRuleType.LegacyFare && item.AirportId == airportId && item.VehicleType == vehicleType && item.IsActive, token);

    private static void Apply(PricingRuleCreateDto request, PricingRule rule)
    {
        rule.AirportId = request.AirportId; rule.VehicleType = request.VehicleType; rule.BasePrice = request.BasePrice;
        rule.AirportPickupSupplement = request.AirportPickupSupplement; rule.FreeWaitingMinutes = request.FreeWaitingMinutes;
        rule.WaitingChargePerHour = request.WaitingChargePerHour; rule.IsActive = request.IsActive;
        rule.RuleType = request.RuleType; rule.Name = request.Name?.Trim() ?? string.Empty; rule.Priority = request.Priority;
        rule.EffectiveFrom = request.EffectiveFrom; rule.EffectiveTo = request.EffectiveTo;
        rule.PickupPostcode = Trim(request.PickupPostcode); rule.DestinationPostcode = Trim(request.DestinationPostcode);
        rule.PickupZone = Trim(request.PickupZone); rule.DestinationZone = Trim(request.DestinationZone); rule.PromotionCode = Trim(request.PromotionCode);
        rule.Amount = request.Amount; rule.Percentage = request.Percentage; rule.UnitRate = request.UnitRate; rule.IncludedUnits = request.IncludedUnits;
    }

    private static IQueryable<PricingRuleListItemDto> RuleQuery(LondonVIPDbContext db, Guid companyId) =>
        db.PricingRules.AsNoTracking().Where(item => item.CompanyId == companyId).Select(item => new PricingRuleListItemDto
        {
            Id = item.Id, AirportId = item.AirportId, AirportCode = item.AirportId == null ? null : db.Airports.Where(airport => airport.Id == item.AirportId).Select(airport => airport.Code).FirstOrDefault(),
            AirportName = item.AirportId == null ? null : db.Airports.Where(airport => airport.Id == item.AirportId).Select(airport => airport.Name).FirstOrDefault(),
            VehicleType = item.VehicleType, BasePrice = item.BasePrice, AirportPickupSupplement = item.AirportPickupSupplement,
            FreeWaitingMinutes = item.FreeWaitingMinutes, WaitingChargePerHour = item.WaitingChargePerHour, IsActive = item.IsActive, UpdatedAt = item.UpdatedAt,
            RuleType = item.RuleType, Name = item.Name, Priority = item.Priority, EffectiveFrom = item.EffectiveFrom, EffectiveTo = item.EffectiveTo
        });

    private static IQueryable<PricingRuleDetailDto> DetailQuery(LondonVIPDbContext db, Guid companyId) =>
        db.PricingRules.AsNoTracking().Where(item => item.CompanyId == companyId).Select(item => new PricingRuleDetailDto
        {
            Id = item.Id, AirportId = item.AirportId, AirportCode = item.AirportId == null ? null : db.Airports.Where(airport => airport.Id == item.AirportId).Select(airport => airport.Code).FirstOrDefault(),
            AirportName = item.AirportId == null ? null : db.Airports.Where(airport => airport.Id == item.AirportId).Select(airport => airport.Name).FirstOrDefault(),
            VehicleType = item.VehicleType, BasePrice = item.BasePrice, AirportPickupSupplement = item.AirportPickupSupplement,
            FreeWaitingMinutes = item.FreeWaitingMinutes, WaitingChargePerHour = item.WaitingChargePerHour, IsActive = item.IsActive,
            CreatedAt = item.CreatedAt, UpdatedAt = item.UpdatedAt, RuleType = item.RuleType, Name = item.Name, Priority = item.Priority,
            EffectiveFrom = item.EffectiveFrom, EffectiveTo = item.EffectiveTo, PickupPostcode = item.PickupPostcode, DestinationPostcode = item.DestinationPostcode,
            PickupZone = item.PickupZone, DestinationZone = item.DestinationZone, PromotionCode = item.PromotionCode,
            Amount = item.Amount, Percentage = item.Percentage, UnitRate = item.UnitRate, IncludedUnits = item.IncludedUnits
        });

    private static async Task<IResult> CloneRuleAsync(Guid id, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, CancellationToken token)
    {
        var source = await db.PricingRules.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && item.CompanyId == company.CompanyId, token);
        if (source is null) { await AuditCrossTenantAsync(db, audit, id, company.CompanyId, token); return Results.NotFound(); }
        var clone = new PricingRule { Id = Guid.NewGuid(), CompanyId = company.CompanyId, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, IsActive = false };
        Apply(ToCreate(source), clone); clone.IsActive = false; clone.Name = string.IsNullOrWhiteSpace(source.Name) ? "Copy" : source.Name + " (copy)";
        db.PricingRules.Add(clone); await db.SaveChangesAsync(token);
        await audit.WriteAsync("PricingRuleCloned", "Pricing", "Succeeded", SecurityEventSeverity.Information, "Pricing rule cloned as inactive.", "PricingRule", clone.Id.ToString(), company.CompanyId, token);
        return Results.Created($"/api/pricing/{clone.Id}", await DetailQuery(db, company.CompanyId).SingleAsync(item => item.Id == clone.Id, token));
    }

    private static async Task<IResult> PreviewAsync(QuoteRequest request, IPricingService pricing, CancellationToken token) => Results.Ok(await pricing.CalculateQuoteAsync(request, token));

    private static PricingRuleCreateDto ToCreate(PricingRule rule) => new()
    {
        AirportId=rule.AirportId,VehicleType=rule.VehicleType,BasePrice=rule.BasePrice,AirportPickupSupplement=rule.AirportPickupSupplement,
        FreeWaitingMinutes=rule.FreeWaitingMinutes,WaitingChargePerHour=rule.WaitingChargePerHour,IsActive=rule.IsActive,RuleType=rule.RuleType,
        Name=rule.Name,Priority=rule.Priority,EffectiveFrom=rule.EffectiveFrom,EffectiveTo=rule.EffectiveTo,PickupPostcode=rule.PickupPostcode,
        DestinationPostcode=rule.DestinationPostcode,PickupZone=rule.PickupZone,DestinationZone=rule.DestinationZone,PromotionCode=rule.PromotionCode,
        Amount=rule.Amount,Percentage=rule.Percentage,UnitRate=rule.UnitRate,IncludedUnits=rule.IncludedUnits
    };

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static async Task AuditCrossTenantAsync(LondonVIPDbContext db, IAuditService audit, Guid id, Guid companyId, CancellationToken cancellationToken)
    {
        if (await db.PricingRules.AnyAsync(item => item.Id == id && item.CompanyId != companyId, cancellationToken))
            await audit.WriteAsync("CrossTenantAccessAttempt", "Authorization", "Denied", SecurityEventSeverity.High, "Cross-tenant pricing rule access was blocked.", "PricingRule", id.ToString(), companyId, cancellationToken);
    }
}
