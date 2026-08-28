using LondonVIP.Infrastructure.Pricing;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Pricing;

namespace LondonVIP.Tests;

public class PricingCalculatorTests
{
    [Fact]
    public void Calculate_ReturnsNotConfigured_WhenNoActiveRuleExists()
    {
        var response = PricingCalculator.Calculate(CreateRequest(), null);

        Assert.False(response.IsConfigured);
        Assert.Equal(PricingCalculator.PricingNotConfiguredMessage, response.Message);
        Assert.Equal(0m, response.TotalFare);
    }

    [Fact]
    public void Calculate_UsesConfiguredBaseFare()
    {
        var response = PricingCalculator.Calculate(CreateRequest(), CreateRule(basePrice: 75m));

        Assert.True(response.IsConfigured);
        Assert.Equal(75m, response.BaseFare);
        Assert.Equal(75m, response.TotalFare);
    }

    [Fact]
    public void Calculate_DoesNotChargeWithinFreeWaitingPeriod()
    {
        var request = CreateRequest(waitingMinutes: 20);
        var response = PricingCalculator.Calculate(
            request,
            CreateRule(basePrice: 75m, freeWaitingMinutes: 30, waitingChargePerHour: 60m));

        Assert.Equal(0, response.ChargeableWaitingMinutes);
        Assert.Equal(0m, response.WaitingCharge);
        Assert.Equal(75m, response.TotalFare);
    }

    [Fact]
    public void Calculate_ChargesOnlyWaitingBeyondFreePeriod()
    {
        var request = CreateRequest(waitingMinutes: 90);
        var response = PricingCalculator.Calculate(
            request,
            CreateRule(basePrice: 75m, freeWaitingMinutes: 30, waitingChargePerHour: 60m));

        Assert.Equal(60, response.ChargeableWaitingMinutes);
        Assert.Equal(60m, response.WaitingCharge);
    }

    [Fact]
    public void Calculate_AddsAirportPickupSupplement_WhenRequested()
    {
        var request = CreateRequest(isAirportPickup: true);
        var response = PricingCalculator.Calculate(
            request,
            CreateRule(basePrice: 75m, airportPickupSupplement: 15m));

        Assert.Equal(15m, response.AirportPickupSupplement);
        Assert.Equal(15m, response.ExtrasTotal);
        Assert.Equal(90m, response.TotalFare);
    }

    [Fact]
    public void Calculate_SumsBaseFareAndAllConfiguredExtras()
    {
        var request = CreateRequest(isAirportPickup: true, waitingMinutes: 60);
        var response = PricingCalculator.Calculate(
            request,
            CreateRule(
                basePrice: 100m,
                airportPickupSupplement: 15m,
                freeWaitingMinutes: 30,
                waitingChargePerHour: 60m));

        Assert.Equal(30, response.ChargeableWaitingMinutes);
        Assert.Equal(30m, response.WaitingCharge);
        Assert.Equal(45m, response.ExtrasTotal);
        Assert.Equal(145m, response.TotalFare);
    }

    private static QuoteRequest CreateRequest(
        bool isAirportPickup = false,
        int waitingMinutes = 0)
    {
        return new QuoteRequest
        {
            PickupAddress = "Test pickup",
            Destination = "Test destination",
            AirportId = null,
            VehicleType = VehicleType.Saloon,
            PassengerCount = 2,
            LuggageCount = 1,
            IsAirportPickup = isAirportPickup,
            WaitingMinutes = waitingMinutes,
            IsMeetAndGreet = false
        };
    }

    private static PricingRule CreateRule(
        decimal basePrice = 0m,
        decimal airportPickupSupplement = 0m,
        int freeWaitingMinutes = 0,
        decimal waitingChargePerHour = 0m)
    {
        return new PricingRule
        {
            Id = Guid.NewGuid(),
            AirportId = null,
            VehicleType = VehicleType.Saloon,
            BasePrice = basePrice,
            AirportPickupSupplement = airportPickupSupplement,
            FreeWaitingMinutes = freeWaitingMinutes,
            WaitingChargePerHour = waitingChargePerHour,
            IsActive = true
        };
    }
}
