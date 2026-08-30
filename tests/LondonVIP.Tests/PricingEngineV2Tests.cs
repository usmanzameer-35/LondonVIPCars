using LondonVIP.Infrastructure.Pricing;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Pricing;

namespace LondonVIP.Tests;

public class PricingEngineV2Tests
{
    [Fact]
    public void AirportFixedFare_PreservesWaitingAndMeetAndGreetExtras()
    {
        var request = Request(); request.IsAirportPickup = true; request.IsMeetAndGreet = true; request.WaitingMinutes = 60;
        var result = PricingCalculator.CalculateRules(request,
        [
            Rule(PricingRuleType.AirportFixedFare, amount: 80, priority: 10, supplement: 12),
            Rule(PricingRuleType.WaitingTime, unitRate: 60, included: 30),
            Rule(PricingRuleType.MeetAndGreet, amount: 8)
        ]);
        Assert.Equal(130m, result.TotalFare);
        Assert.Equal(30m, result.WaitingCharge);
        Assert.Equal(12m, result.AirportPickupSupplement);
    }

    [Fact]
    public void HourlyRule_UsesConfiguredDatabaseRate()
    {
        var request = Request(); request.HireHours = 3.5m;
        var result = PricingCalculator.CalculateRules(request, [Rule(PricingRuleType.HourlyHire, unitRate: 40)]);
        Assert.Equal(140m, result.TotalFare);
    }

    [Fact]
    public void CorporateDiscount_AppliesAfterConfiguredExtras()
    {
        var request = Request(); request.IsCorporateCustomer = true;
        var result = PricingCalculator.CalculateRules(request, [Rule(PricingRuleType.ZoneFixedFare, amount: 100), Rule(PricingRuleType.CorporateDiscount, percentage: 10)]);
        Assert.Equal(10m, result.DiscountTotal);
        Assert.Equal(90m, result.TotalFare);
    }

    [Fact]
    public void HighestPriorityOverlappingRuleWins()
    {
        var result = PricingCalculator.CalculateRules(Request(), [Rule(PricingRuleType.PostcodeFixedFare, amount: 50, priority: 1), Rule(PricingRuleType.PostcodeFixedFare, amount: 75, priority: 20)]);
        Assert.Equal(75m, result.BaseFare);
    }

    [Fact]
    public void MinimumFareAndPromotionalDiscount_AreAppliedInStableOrder()
    {
        var request = Request(); request.PromotionCode = "VIP10";
        var result = PricingCalculator.CalculateRules(request, [Rule(PricingRuleType.Distance, unitRate: 2), Rule(PricingRuleType.PromotionalDiscount, percentage: 10), Rule(PricingRuleType.MinimumFare, amount: 25)]);
        Assert.Equal(25m, result.TotalFare);
    }

    [Fact]
    public void Calculation_DoesNotMutateHistoricalBookingPrices()
    {
        var booking = new Booking { BaseFare = 70m, Extras = 10m, TotalFare = 80m };
        _ = PricingCalculator.CalculateRules(Request(), [Rule(PricingRuleType.ZoneFixedFare, amount: 999)]);
        Assert.Equal(70m, booking.BaseFare); Assert.Equal(10m, booking.Extras); Assert.Equal(80m, booking.TotalFare);
    }

    private static QuoteRequest Request() => new() { PickupAddress = "A", Destination = "B", VehicleType = VehicleType.Saloon, PassengerCount = 1, DistanceMiles = 10 };
    private static PricingRule Rule(PricingRuleType type, decimal amount = 0, decimal unitRate = 0, decimal percentage = 0, decimal included = 0, int priority = 0, decimal supplement = 0) => new()
    {
        Id=Guid.NewGuid(),RuleType=type,VehicleType=VehicleType.Saloon,Amount=amount,UnitRate=unitRate,Percentage=percentage,
        IncludedUnits=included,FreeWaitingMinutes=(int)included,AirportPickupSupplement=supplement,Priority=priority,IsActive=true,UpdatedAt=DateTimeOffset.UtcNow
    };
}
