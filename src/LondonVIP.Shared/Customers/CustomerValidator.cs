using System.Text.RegularExpressions;

namespace LondonVIP.Shared.Customers;

public static partial class CustomerValidator
{
    public static Dictionary<string, string[]> Validate(CustomerCreateDto? customer)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        void Add(string field, string message) { if (!errors.TryGetValue(field, out var list)) errors[field] = list = []; list.Add(message); }
        void Maximum(string field, string? value, int length) { if (value?.Length > length) Add(field, $"Value cannot exceed {length} characters."); }
        if (customer is null) { Add(string.Empty, "Customer data is required."); return Result(errors); }
        if (string.IsNullOrWhiteSpace(customer.FirstName)) Add("firstName", "First name is required.");
        if (string.IsNullOrWhiteSpace(customer.LastName)) Add("lastName", "Last name is required.");
        if (string.IsNullOrWhiteSpace(customer.Email) && string.IsNullOrWhiteSpace(customer.Phone)) Add("contact", "An email address or phone number is required.");
        Maximum("firstName", customer.FirstName, 100); Maximum("lastName", customer.LastName, 100);
        Maximum("email", customer.Email, 254); Maximum("phone", customer.Phone, 30); Maximum("secondaryPhone", customer.SecondaryPhone, 30);
        Maximum("address", customer.Address, 500); Maximum("postcode", customer.Postcode, 20); Maximum("notes", customer.Notes, 4000);
        if (!string.IsNullOrWhiteSpace(customer.Email) && !EmailPattern().IsMatch(customer.Email.Trim())) Add("email", "Email must be a valid address.");
        foreach (var phone in new[] { ("phone", customer.Phone), ("secondaryPhone", customer.SecondaryPhone) })
            if (!string.IsNullOrWhiteSpace(phone.Item2) && !PhonePattern().IsMatch(phone.Item2.Trim())) Add(phone.Item1, "Phone contains unsupported characters or is outside the permitted length.");
        return Result(errors);
    }

    private static Dictionary<string, string[]> Result(Dictionary<string, List<string>> errors) => errors.ToDictionary(item => item.Key, item => item.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailPattern();
    [GeneratedRegex(@"^[0-9+() .-]{7,30}$")]
    private static partial Regex PhonePattern();
}
