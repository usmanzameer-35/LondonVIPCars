namespace LondonVIP.Shared.Models;

public class Notification
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public string Recipient { get; set; } = string.Empty;
    public NotificationRecipientType RecipientType { get; set; }
    public NotificationType NotificationType { get; set; }
    public NotificationChannel Channel { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public int RetryCount { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}
public enum NotificationStatus { Pending, Sent, Failed, Cancelled }
public enum NotificationChannel { Email, Sms, InternalErp }
public enum NotificationRecipientType { Customer, Driver, CorporateAccount, User }
public enum NotificationType
{
    BookingCreated,BookingConfirmed,BookingCancelled,DriverAssigned,DriverUnassigned,DriverEnRoute,DriverArrived,PassengerOnboard,BookingCompleted,NoShow,UnableToComplete,
    QuoteCreated,QuoteExpiresSoon,QuoteAccepted,QuoteConverted,QuoteCancelled,InvoiceGenerated,PaymentReceived,PaymentReminder
}
