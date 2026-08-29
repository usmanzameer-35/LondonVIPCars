using System.Net.Mail;
using System.Text.RegularExpressions;
using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.Drivers;

public static partial class DriverValidator
{
    public static Dictionary<string, string[]> Validate(DriverCreateDto value)
    {
        var errors = new Dictionary<string, List<string>>();
        AddRequired(errors, "firstName", value.FirstName, 100);
        AddRequired(errors, "lastName", value.LastName, 100);
        if (string.IsNullOrWhiteSpace(value.Phone) && string.IsNullOrWhiteSpace(value.Email)) Add(errors, "contact", "A phone number or email address is required.");
        if (!string.IsNullOrWhiteSpace(value.Email) && (!MailAddress.TryCreate(value.Email, out _) || value.Email.Length > 254)) Add(errors, "email", "Enter a valid email address.");
        if (!string.IsNullOrWhiteSpace(value.Phone) && (value.Phone.Length is < 7 or > 30 || !PhoneRegex().IsMatch(value.Phone))) Add(errors, "phone", "Phone must contain 7 to 30 valid telephone characters.");
        CheckLength(errors, "driverNumber", value.DriverNumber, 50); CheckLength(errors, "notes", value.Notes, 4000);
        CheckLength(errors, "drivingLicenceNumber", value.DrivingLicenceNumber, 100); CheckLength(errors, "privateHireLicenceNumber", value.PrivateHireLicenceNumber, 100);
        if (!Enum.IsDefined(value.AvailabilityStatus)) Add(errors, "availabilityStatus", "Availability status is invalid.");
        return errors.ToDictionary(item => item.Key, item => item.Value.ToArray());
    }
    private static void AddRequired(Dictionary<string,List<string>> e,string k,string? v,int max) { if(string.IsNullOrWhiteSpace(v)) Add(e,k,"This field is required."); else if(v.Trim().Length>max) Add(e,k,$"Maximum length is {max} characters."); }
    private static void CheckLength(Dictionary<string,List<string>> e,string k,string? v,int max) { if(v?.Length>max) Add(e,k,$"Maximum length is {max} characters."); }
    private static void Add(Dictionary<string,List<string>> e,string k,string m) { if(!e.TryGetValue(k,out var list)) e[k]=list=[]; list.Add(m); }
    [GeneratedRegex(@"^[0-9+() .'-]+$")] private static partial Regex PhoneRegex();
}
