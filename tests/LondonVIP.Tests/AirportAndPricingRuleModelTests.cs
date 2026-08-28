using LondonVIP.Shared.Models;

namespace LondonVIP.Tests;

public class AirportAndPricingRuleModelTests
{
    [Fact]
    public void Airport_CanBeInstantiated()
    {
        var airportId = Guid.NewGuid();

        var airport = new Airport
        {
            Id = airportId,
            Code = "TEST",
            Name = "Test Airport",
            IsActive = true
        };

        Assert.Equal(airportId, airport.Id);
        Assert.Equal("TEST", airport.Code);
        Assert.Equal("Test Airport", airport.Name);
        Assert.True(airport.IsActive);
    }

    [Fact]
    public void PricingRule_CanBeInstantiated()
    {
        var ruleId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var pricingRule = new PricingRule
        {
            Id = ruleId,
            CompanyId = companyId,
            AirportId = null,
            VehicleType = VehicleType.Saloon,
            BasePrice = 0m,
            AirportPickupSupplement = 0m,
            FreeWaitingMinutes = 0,
            WaitingChargePerHour = 0m,
            IsActive = true
        };

        Assert.Equal(ruleId, pricingRule.Id);
        Assert.Equal(companyId, pricingRule.CompanyId);
        Assert.Null(pricingRule.AirportId);
        Assert.Equal(VehicleType.Saloon, pricingRule.VehicleType);
        Assert.Equal(0m, pricingRule.BasePrice);
        Assert.Equal(0m, pricingRule.AirportPickupSupplement);
        Assert.Equal(0, pricingRule.FreeWaitingMinutes);
        Assert.Equal(0m, pricingRule.WaitingChargePerHour);
        Assert.True(pricingRule.IsActive);
    }
}
