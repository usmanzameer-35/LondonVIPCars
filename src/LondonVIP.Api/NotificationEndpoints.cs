using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Security;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Notifications;
using LondonVIP.Shared.Security;
using LondonVIP.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;
namespace LondonVIP.Api;
public static class NotificationEndpoints
{
 public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
 {var g=endpoints.MapGroup("/api/notifications").RequireAuthorization(SecurityPolicies.CompanyAdministration).RequireRateLimiting("operations");g.MapGet("",List);g.MapGet("/{id:guid}",Get);g.MapPost("/{id:guid}/retry",Retry);g.MapPost("/{id:guid}/resend",Retry);endpoints.MapGet("/api/customer-portal/{customerId:guid}/notifications",Portal).RequireAuthorization(SecurityPolicies.CustomerRead).RequireRateLimiting("operations");return endpoints;}
 private static async Task<IResult> List(LondonVIPDbContext db,ICompanyContext company,int? status,string? search,CancellationToken token){var q=db.Notifications.AsNoTracking().Where(x=>x.CompanyId==company.CompanyId);if(status.HasValue)q=q.Where(x=>(int)x.Status==status);if(!string.IsNullOrWhiteSpace(search))q=q.Where(x=>x.Recipient.Contains(search)||x.Subject.Contains(search));var data=await q.ToListAsync(token);return Results.Ok(data.OrderByDescending(x=>x.CreatedAt).Select(ToDto));}
 private static async Task<IResult> Get(Guid id,LondonVIPDbContext db,ICompanyContext company,CancellationToken token){var x=await db.Notifications.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id&&x.CompanyId==company.CompanyId,token);return x is null?Results.NotFound():Results.Ok(ToDto(x));}
 private static async Task<IResult> Retry(Guid id,INotificationService service,LondonVIPDbContext db,ICompanyContext company,IAuditService audit,CancellationToken token){if(!await db.Notifications.AnyAsync(x=>x.Id==id&&x.CompanyId==company.CompanyId,token))return Results.NotFound();var sent=await service.SendAsync(id,token);await audit.WriteAsync("NotificationResent","Notifications",sent?"Succeeded":"Failed",sent?SecurityEventSeverity.Information:SecurityEventSeverity.Warning,"Manual notification delivery attempted.","Notification",id.ToString(),company.CompanyId,token);return sent?Results.Ok():Results.Problem("Notification delivery failed.");}
 private static async Task<IResult> Portal(Guid customerId,LondonVIPDbContext db,ICompanyContext company,CancellationToken token){var customer=await db.Customers.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==customerId&&x.CompanyId==company.CompanyId,token);if(customer is null)return Results.NotFound();var recipients=new[]{customer.Email,customer.Phone,customerId.ToString()};var data=await db.Notifications.AsNoTracking().Where(x=>x.CompanyId==company.CompanyId&&x.RecipientType==NotificationRecipientType.Customer&&recipients.Contains(x.Recipient)).ToListAsync(token);return Results.Ok(data.OrderByDescending(x=>x.CreatedAt).Take(20).Select(ToDto));}
 private static NotificationDto ToDto(Notification x)=>new(){Id=x.Id,Recipient=x.Recipient,RecipientType=x.RecipientType,NotificationType=x.NotificationType,Channel=x.Channel,Subject=x.Subject,Body=x.Body,TemplateName=x.TemplateName,Status=x.Status,CreatedAt=x.CreatedAt,SentAt=x.SentAt,RetryCount=x.RetryCount,CorrelationId=x.CorrelationId};
}
