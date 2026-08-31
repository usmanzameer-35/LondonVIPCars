using LondonVIP.Infrastructure.Data;
using LondonVIP.Shared.Growth;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LondonVIP.Infrastructure.Growth;

public sealed class PromotionEngine(LondonVIPDbContext db, ICompanyContext company) : IPromotionEngine
{
    public async Task<PromotionValidationResult> ValidateAsync(PromotionContext context, CancellationToken token = default)
    {
        var code = context.Code.Trim().ToUpperInvariant();
        var now = DateTimeOffset.UtcNow;
        var promotion = await db.Promotions.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.Code == code && x.IsActive &&
            x.EffectiveFrom <= now && (x.EffectiveTo == null || x.EffectiveTo >= now)).SingleOrDefaultAsync(token);
        if (promotion is null) return new(false, "Promotion is invalid, inactive or expired.");
        if (promotion.MinimumSpend is { } minimum && context.Fare < minimum) return new(false, "Minimum spend has not been reached.");
        if (promotion.FirstBookingOnly && context.PriorBookings != 0) return new(false, "Promotion is limited to first bookings.");
        if (promotion.ReturningCustomersOnly && context.PriorBookings == 0) return new(false, "Promotion is limited to returning customers.");
        if (promotion.CorporateOnly && context.CorporateAccountId is null) return new(false, "Promotion requires a corporate account.");
        if (promotion.AirportId is { } airportId && context.AirportId != airportId) return new(false, "Promotion does not apply to this airport.");
        if (!Matches(promotion.PickupPattern, context.Pickup) || !Matches(promotion.DestinationPattern, context.Destination)) return new(false, "Promotion does not apply to this route.");
        if (promotion.UsageLimit is { } limit && await db.PromotionRedemptions.CountAsync(x => x.CompanyId == company.CompanyId && x.PromotionId == promotion.Id, token) >= limit)
            return new(false, "Promotion usage limit has been reached.");
        if (context.CustomerId is { } customerId && promotion.PerCustomerLimit is { } customerLimit &&
            await db.PromotionRedemptions.CountAsync(x => x.CompanyId == company.CompanyId && x.PromotionId == promotion.Id && x.CustomerId == customerId, token) >= customerLimit)
            return new(false, "Customer usage limit has been reached.");
        var discount = promotion.Kind == DiscountKind.Percentage ? context.Fare * promotion.Value / 100m : promotion.Value;
        discount = Math.Min(context.Fare, promotion.MaximumDiscount is { } maximum ? Math.Min(discount, maximum) : discount);
        return new(true, "Promotion applied.", promotion.Id, decimal.Round(discount, 2, MidpointRounding.AwayFromZero));
    }
    private static bool Matches(string? pattern, string value) => string.IsNullOrWhiteSpace(pattern) || value.Contains(pattern, StringComparison.OrdinalIgnoreCase);
}

public sealed class ReferralService(LondonVIPDbContext db, ICompanyContext company) : IReferralService
{
    public async Task<ReferralDto> CreateAsync(ReferralRequest request, CancellationToken token = default)
    {
        var entity = new Referral { Id = Guid.NewGuid(), CompanyId = company.CompanyId, Code = $"REF-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            ReferrerType = request.ReferrerType.Trim(), ReferrerId = request.ReferrerId, RewardAmount = request.RewardAmount, Status = ReferralStatus.Pending, CreatedAt = DateTimeOffset.UtcNow };
        db.Referrals.Add(entity); await db.SaveChangesAsync(token); return Map(entity);
    }
    public async Task<bool> QualifyAsync(Guid id, Guid customerId, CancellationToken token = default)
    {
        var entity = await db.Referrals.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == company.CompanyId, token);
        if (entity is null || entity.Status != ReferralStatus.Pending) return false;
        if (!await db.Customers.AnyAsync(x => x.Id == customerId && x.CompanyId == company.CompanyId, token)) return false;
        entity.ReferredCustomerId = customerId; entity.Status = ReferralStatus.Qualified; entity.QualifiedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(token); return true;
    }
    private static ReferralDto Map(Referral x) => new(x.Id, x.Code, $"/refer/{x.Code}", x.ReferrerType, x.Status, x.RewardAmount, x.CreatedAt);
}

public sealed class LoyaltyService(LondonVIPDbContext db, ICompanyContext company) : ILoyaltyService
{
    public async Task<LoyaltySummaryDto?> GetAsync(Guid customerId, CancellationToken token = default)
    {
        var account = await db.LoyaltyAccounts.AsNoTracking().Include(x => x.Transactions).SingleOrDefaultAsync(x => x.CompanyId == company.CompanyId && x.CustomerId == customerId, token);
        return account is null ? null : Map(account);
    }
    public async Task<LoyaltySummaryDto> ChangePointsAsync(LoyaltyChangeRequest request, CancellationToken token = default)
    {
        if (!await db.Customers.AnyAsync(x => x.Id == request.CustomerId && x.CompanyId == company.CompanyId, token)) throw new InvalidOperationException("Customer was not found.");
        var account = await db.LoyaltyAccounts.Include(x => x.Transactions).SingleOrDefaultAsync(x => x.CompanyId == company.CompanyId && x.CustomerId == request.CustomerId, token)
            ?? new LoyaltyAccount { Id = Guid.NewGuid(), CompanyId = company.CompanyId, CustomerId = request.CustomerId };
        if (account.PointsBalance + request.Points < 0) throw new InvalidOperationException("Insufficient loyalty points.");
        if (db.Entry(account).State == EntityState.Detached) db.LoyaltyAccounts.Add(account);
        account.PointsBalance += request.Points; if (request.Points > 0) account.LifetimePoints += request.Points; account.Tier = Tier(account.LifetimePoints); account.UpdatedAt = DateTimeOffset.UtcNow;
        account.Transactions.Add(new LoyaltyTransaction { Id = Guid.NewGuid(), CompanyId = company.CompanyId, Points = request.Points, Reason = request.Reason.Trim(), BookingId = request.BookingId, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(token); return Map(account);
    }
    private static LoyaltyTier Tier(int points) => points >= 10000 ? LoyaltyTier.Vip : points >= 5000 ? LoyaltyTier.Gold : points >= 2000 ? LoyaltyTier.Silver : LoyaltyTier.Bronze;
    private static LoyaltySummaryDto Map(LoyaltyAccount x) => new(x.CustomerId, x.PointsBalance, x.LifetimePoints, x.Tier, x.Transactions.OrderByDescending(t => t.CreatedAt).Select(t => new LoyaltyTransactionDto(t.Points, t.Reason, t.VoucherCode, t.CreatedAt)).ToList());
}
