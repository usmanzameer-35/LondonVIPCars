using LondonVIP.Infrastructure.Data;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Notifications;
using LondonVIP.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
namespace LondonVIP.Infrastructure.Notifications;

public sealed class NotificationService(LondonVIPDbContext db,ICompanyContext company,IEnumerable<INotificationProvider> providers,ILogger<NotificationService> logger):INotificationService
{
    public async Task QueueAsync(NotificationRequest request,CancellationToken token=default)
    {try{db.Notifications.Add(new Notification{Id=Guid.NewGuid(),CompanyId=company.CompanyId,Recipient=request.Recipient,RecipientType=request.RecipientType,NotificationType=request.NotificationType,Channel=request.Channel,Subject=request.Subject,Body=request.Body,TemplateName=request.TemplateName,Status=NotificationStatus.Pending,CreatedAt=DateTimeOffset.UtcNow,CorrelationId=request.CorrelationId??Guid.NewGuid().ToString("N")});await db.SaveChangesAsync(token);}catch(Exception exception){logger.LogError(exception,"Notification queueing failed after the business operation completed.");}}
    public async Task<bool> SendAsync(Guid id,CancellationToken token=default)
    {var item=await db.Notifications.SingleOrDefaultAsync(x=>x.Id==id&&x.CompanyId==company.CompanyId,token);if(item is null||item.Status==NotificationStatus.Cancelled)return false;item.RetryCount++;try{var provider=providers.Single(x=>x.Channel==item.Channel);await provider.SendAsync(item,token);item.Status=NotificationStatus.Sent;item.SentAt=DateTimeOffset.UtcNow;}catch(Exception exception){item.Status=NotificationStatus.Failed;logger.LogError(exception,"Development notification delivery failed for {NotificationId}.",item.Id);}await db.SaveChangesAsync(token);return item.Status==NotificationStatus.Sent;}
}
public abstract class DevelopmentNotificationProvider(ILogger logger):INotificationProvider
{public abstract NotificationChannel Channel{get;}public Task SendAsync(Notification notification,CancellationToken token=default){logger.LogInformation("Development {Channel} notification {NotificationId} recorded for delivery.",Channel,notification.Id);return Task.CompletedTask;}}
public sealed class DevelopmentEmailProvider(ILogger<DevelopmentEmailProvider> logger):DevelopmentNotificationProvider(logger),IEmailProvider{public override NotificationChannel Channel=>NotificationChannel.Email;}
public sealed class DevelopmentSmsProvider(ILogger<DevelopmentSmsProvider> logger):DevelopmentNotificationProvider(logger),ISmsProvider{public override NotificationChannel Channel=>NotificationChannel.Sms;}
public sealed class DevelopmentInternalProvider(ILogger<DevelopmentInternalProvider> logger):DevelopmentNotificationProvider(logger){public override NotificationChannel Channel=>NotificationChannel.InternalErp;}
