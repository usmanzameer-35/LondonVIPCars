using LondonVIP.Shared.Models;
namespace LondonVIP.Shared.Notifications;

public sealed record NotificationRequest(string Recipient,NotificationRecipientType RecipientType,NotificationType NotificationType,string Subject,string Body,string TemplateName,NotificationChannel Channel=NotificationChannel.InternalErp,string? CorrelationId=null);
public sealed class NotificationDto
{
    public Guid Id{get;set;}public string Recipient{get;set;}=string.Empty;public NotificationRecipientType RecipientType{get;set;}public NotificationType NotificationType{get;set;}public NotificationChannel Channel{get;set;}public string Subject{get;set;}=string.Empty;public string Body{get;set;}=string.Empty;public string TemplateName{get;set;}=string.Empty;public NotificationStatus Status{get;set;}public DateTimeOffset CreatedAt{get;set;}public DateTimeOffset? SentAt{get;set;}public int RetryCount{get;set;}public string CorrelationId{get;set;}=string.Empty;
}
public interface INotificationService
{
    Task QueueAsync(NotificationRequest request,CancellationToken token=default);
    Task<bool> SendAsync(Guid id,CancellationToken token=default);
}
public interface INotificationProvider { NotificationChannel Channel{get;} Task SendAsync(Notification notification,CancellationToken token=default); }
public interface IEmailProvider : INotificationProvider;
public interface ISmsProvider : INotificationProvider;
