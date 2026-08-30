using LondonVIP.Shared.Payments;

namespace LondonVIP.Shared.Payments;

public static class PaymentValidator
{
    public static Dictionary<string, string[]> Validate(PaymentCreateDto request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.PaymentReference))
            Add(errors, "paymentReference", "Payment reference is required.");

        if (request.Amount <= 0)
            Add(errors, "amount", "Amount must be greater than zero.");

        if (string.IsNullOrWhiteSpace(request.PaymentMethod))
            Add(errors, "paymentMethod", "Payment method is required.");

        var validMethods = new[] { "Cash", "BankTransfer", "Card", "Cheque", "Other" };
        if (!validMethods.Contains(request.PaymentMethod))
            Add(errors, "paymentMethod", $"Payment method must be one of: {string.Join(", ", validMethods)}");

        if (!request.CorporateAccountId.HasValue && !request.CustomerId.HasValue)
            Add(errors, "recipient", "Payment must be associated with either a corporate account or a customer.");

        return errors;
    }

    public static Dictionary<string, string[]> ValidateAllocation(PaymentAllocationCreateDto request, decimal paymentAmount, decimal allocatedAmount)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.Amount <= 0)
            Add(errors, "amount", "Allocation amount must be greater than zero.");

        var availableAmount = paymentAmount - allocatedAmount;
        if (request.Amount > availableAmount)
            Add(errors, "amount", $"Allocation amount cannot exceed available amount of {availableAmount:C}.");

        return errors;
    }

    private static void Add(Dictionary<string, string[]> errors, string key, string message)
    {
        if (!errors.ContainsKey(key))
            errors[key] = [message];
        else
            errors[key] = [.. errors[key], message];
    }
}
