using LondonVIP.Shared.Invoices;

namespace LondonVIP.Shared.Invoices;

public static class InvoiceValidator
{
    public static Dictionary<string, string[]> Validate(InvoiceCreateDto request, Guid companyId, DateTimeOffset now)
    {
        var errors = new Dictionary<string, string[]>();

        // At least one recipient (account or customer)
        if (!request.CorporateAccountId.HasValue && !request.CustomerId.HasValue)
            Add(errors, "recipient", "Invoice must be for either a corporate account or a customer.");

        // Must have at least one line
        if (request.Lines.Count == 0)
            Add(errors, "lines", "Invoice must have at least one line item.");

        // Validate each line
        for (int i = 0; i < request.Lines.Count; i++)
        {
            var line = request.Lines[i];
            var linePrefix = $"lines[{i}]";

            if (string.IsNullOrWhiteSpace(line.Description))
                Add(errors, $"{linePrefix}.description", "Description is required.");

            if (line.Quantity <= 0)
                Add(errors, $"{linePrefix}.quantity", "Quantity must be greater than zero.");

            if (line.UnitPrice < 0)
                Add(errors, $"{linePrefix}.unitPrice", "Unit price cannot be negative.");

            if (line.TaxRate < 0 || line.TaxRate > 100)
                Add(errors, $"{linePrefix}.taxRate", "Tax rate must be between 0 and 100.");
        }

        // Date validation
        var invoiceDate = request.InvoiceDate ?? now;
        if (request.DueDate.HasValue && request.DueDate.Value < invoiceDate)
            Add(errors, "dueDate", "Due date must be on or after invoice date.");

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
