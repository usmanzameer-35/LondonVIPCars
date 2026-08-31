using LondonVIP.Shared.Models;
using LondonVIP.Shared.Pricing;

namespace LondonVIP.Shared.CustomerPortal;

public sealed record CustomerRegistrationRequest(string CompanySlug, string FirstName, string LastName, string Email, string Phone, string Password);
public sealed record CustomerEmailTokenRequest(string CompanySlug, string Email, string Token);
public sealed record CustomerForgotPasswordRequest(string CompanySlug, string Email);
public sealed record CustomerResetPasswordRequest(string CompanySlug, string Email, string Token, string NewPassword);
public sealed record CustomerProfileUpdateRequest(string FirstName, string LastName, string Email, string Phone, string? SecondaryPhone, string? Address, string? Postcode);
public sealed record CustomerAddressRequest(string Label, string AddressLine1, string? AddressLine2, string City, string Postcode, bool IsFavourite, bool IsDefault);
public sealed record CustomerAddressDto(Guid Id, string Label, string AddressLine1, string? AddressLine2, string City, string Postcode, bool IsFavourite, bool IsDefault);
public sealed record CustomerPreferencesDto(string? EmergencyContactName, string? EmergencyContactPhone, bool MarketingEnabled, bool EmailNotifications, bool SmsNotifications, string Language);
public sealed record CustomerBookingAmendRequest(DateTimeOffset PickupDateTime, string PickupAddress, string Destination, int PassengerCount, int LuggageCount, string? Notes);
public sealed record CustomerBookingCancelRequest(string Reason);
public sealed record CustomerPaymentIntentRequest(Guid InvoiceId, decimal Amount, string Method, string IdempotencyKey);
public sealed record CustomerPaymentIntentResult(bool Available, string? IntentReference, string Status, string? ClientSecret, string? Message);
public sealed record CustomerQuoteCreateRequest(QuoteRequest Pricing, DateTimeOffset PickupDateTime, Guid? CorporateAccountId, string? FlightNumber, string? Notes);

public interface ICustomerIdentityResolver { Task<Guid?> GetCustomerIdAsync(CancellationToken token = default); }
public interface IPaymentGateway
{
    string ProviderName { get; }
    IReadOnlySet<string> SupportedMethods { get; }
    Task<CustomerPaymentIntentResult> CreateIntentAsync(CustomerPaymentIntentRequest request, CancellationToken token = default);
    Task<CustomerPaymentIntentResult> RefundAsync(string paymentReference, decimal amount, CancellationToken token = default);
}
