using LondonVIP.Shared.CustomerPortal;
using LondonVIP.Shared.Integrations;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Notifications;

namespace LondonVIP.Infrastructure.Integrations;

public sealed class StripeCustomerPaymentGateway(StripePaymentProvider stripe) : IPaymentGateway
{
    public string ProviderName => "Stripe";
    public IReadOnlySet<string> SupportedMethods { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Card", "ApplePay", "GooglePay", "SavedCard" };
    public async Task<CustomerPaymentIntentResult> CreateIntentAsync(CustomerPaymentIntentRequest request, CancellationToken token = default)
    {
        return await stripe.CreateCustomerIntentAsync(request, token);
    }
    public async Task<CustomerPaymentIntentResult> RefundAsync(string paymentReference, decimal amount, CancellationToken token = default)
    {
        var result = await stripe.RefundAsync(new RefundRequest(paymentReference, amount, $"refund:{paymentReference}:{amount}"), token);
        return result.Success ? new(true, result.ProviderReference, "pending", null, null) : new(false, null, "Unavailable", null, result.Error);
    }
}

public abstract class ProductionNotificationBridge(ICommunicationProvider provider) : INotificationProvider
{
    public abstract NotificationChannel Channel { get; }
    public async Task SendAsync(Notification notification, CancellationToken token = default)
    {
        var result = await provider.SendAsync(new CommunicationRequest(notification.Recipient, notification.TemplateName, new Dictionary<string, string> { ["subject"] = notification.Subject, ["body"] = notification.Body }, notification.CorrelationId), token);
        if (!result.Accepted) throw new InvalidOperationException(result.Error ?? $"{provider.Key} rejected the notification.");
    }
}
public sealed class SendGridNotificationBridge(SendGridEmailProvider provider) : ProductionNotificationBridge(provider), IEmailProvider { public override NotificationChannel Channel => NotificationChannel.Email; }
public sealed class TwilioSmsNotificationBridge(TwilioSmsProvider provider) : ProductionNotificationBridge(provider), ISmsProvider { public override NotificationChannel Channel => NotificationChannel.Sms; }
