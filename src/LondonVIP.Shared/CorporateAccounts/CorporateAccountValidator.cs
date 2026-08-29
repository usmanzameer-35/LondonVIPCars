using System.Net.Mail;
using System.Text.RegularExpressions;
using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.CorporateAccounts;

public static partial class CorporateAccountValidator
{
    public static string NormalizeAccountNumber(string value) => string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();
    public static Dictionary<string,string[]> Validate(CorporateAccountCreateDto value)
    {
        var errors=new Dictionary<string,string[]>();
        Required(errors,"accountNumber",value.AccountNumber,30); Required(errors,"accountName",value.AccountName,200); Required(errors,"primaryContactName",value.PrimaryContactName,150); Required(errors,"addressLine1",value.AddressLine1,250); Required(errors,"townCity",value.TownCity,100); Required(errors,"postcode",value.Postcode,20); Required(errors,"country",value.Country,100);
        if(string.IsNullOrWhiteSpace(value.Email)||!MailAddress.TryCreate(value.Email,out _)) errors["email"]=["Enter a valid email address."];
        if(!string.IsNullOrWhiteSpace(value.BillingEmail)&&!MailAddress.TryCreate(value.BillingEmail,out _)) errors["billingEmail"]=["Enter a valid billing email address."];
        if(string.IsNullOrWhiteSpace(value.Phone)||value.Phone.Length is < 7 or > 30||!Phone().IsMatch(value.Phone)) errors["phone"]=["Enter a valid phone number."];
        if(!Enum.IsDefined(value.BillingTerms)) errors["billingTerms"]=["Billing terms are invalid."];
        if(value.CreditLimit<0) errors["creditLimit"]=["Credit limit cannot be negative."];
        Limit(errors,"tradingName",value.TradingName,200);Limit(errors,"addressLine2",value.AddressLine2,250);Limit(errors,"defaultPurchaseOrderReference",value.DefaultPurchaseOrderReference,100);Limit(errors,"notes",value.Notes,4000);
        return errors;
    }
    private static void Required(Dictionary<string,string[]> e,string k,string? v,int m){if(string.IsNullOrWhiteSpace(v)||v.Trim().Length>m)e[k]=[$"This field is required and must not exceed {m} characters."];}
    private static void Limit(Dictionary<string,string[]> e,string k,string? v,int m){if(v?.Length>m)e[k]=[$"Maximum length is {m} characters."];}
    [GeneratedRegex(@"^[0-9+() .'-]+$")]private static partial Regex Phone();
}
