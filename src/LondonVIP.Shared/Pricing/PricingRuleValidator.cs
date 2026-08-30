namespace LondonVIP.Shared.Pricing;

public static class PricingRuleValidator
{
    public static Dictionary<string, string[]> Validate(PricingRuleCreateDto? rule)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (rule is null) { errors[string.Empty] = ["Pricing rule data is required."]; return errors; }
        if (!Enum.IsDefined(rule.VehicleType)) errors["vehicleType"] = ["Vehicle type is invalid."];
        if (!Enum.IsDefined(rule.RuleType)) errors["ruleType"] = ["Rule type is invalid."];
        if (rule.Name?.Length > 200) errors["name"] = ["Name cannot exceed 200 characters."];
        if (rule.Priority is < -10000 or > 10000) errors["priority"] = ["Priority must be between -10,000 and 10,000."];
        if (rule.EffectiveFrom.HasValue && rule.EffectiveTo.HasValue && rule.EffectiveTo <= rule.EffectiveFrom) errors["effectiveTo"] = ["Effective To must be later than Effective From."];
        if (rule.Amount is < -100000 or > 100000) errors["amount"] = ["Amount must be between -100,000 and 100,000."];
        if (rule.Percentage is < 0 or > 100) errors["percentage"] = ["Percentage must be between 0 and 100."];
        if (rule.UnitRate is < 0 or > 100000) errors["unitRate"] = ["Unit rate must be between 0 and 100,000."];
        if (rule.IncludedUnits is < 0 or > 100000) errors["includedUnits"] = ["Included units must be between 0 and 100,000."];
        if (rule.BasePrice is < 0 or > 100000) errors["basePrice"] = ["Base price must be between 0 and 100,000."];
        if (rule.AirportPickupSupplement is < 0 or > 10000) errors["airportPickupSupplement"] = ["Airport pickup supplement must be between 0 and 10,000."];
        if (rule.FreeWaitingMinutes is < 0 or > 1440) errors["freeWaitingMinutes"] = ["Free waiting minutes must be between 0 and 1,440."];
        if (rule.WaitingChargePerHour is < 0 or > 10000) errors["waitingChargePerHour"] = ["Waiting charge per hour must be between 0 and 10,000."];
        if (DecimalPlaces(rule.BasePrice) > 2) errors["basePrice"] = ["Base price cannot have more than two decimal places."];
        if (DecimalPlaces(rule.AirportPickupSupplement) > 2) errors["airportPickupSupplement"] = ["Airport pickup supplement cannot have more than two decimal places."];
        if (DecimalPlaces(rule.WaitingChargePerHour) > 2) errors["waitingChargePerHour"] = ["Waiting charge per hour cannot have more than two decimal places."];
        return errors;
    }

    private static int DecimalPlaces(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0x7F;
}
