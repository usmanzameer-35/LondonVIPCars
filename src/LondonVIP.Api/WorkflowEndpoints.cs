using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Security;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Security;
using LondonVIP.Shared.Tenancy;
using LondonVIP.Shared.Workflows;
using Microsoft.EntityFrameworkCore;
namespace LondonVIP.Api;
public static class WorkflowEndpoints
{
 public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder e){var g=e.MapGroup("/api/workflows").RequireAuthorization(SecurityPolicies.ErpAccess).RequireRateLimiting("operations");g.MapGet("/dashboard",Dashboard);g.MapGet("/jobs",Jobs);g.MapGet("/history",History);g.MapGet("/reminders",Reminders);g.MapGet("/events",Events);g.MapGet("/rules",Rules);g.MapPost("/jobs/{id:guid}/retry",Retry);g.MapPost("/jobs/{id:guid}/cancel",Cancel);g.MapPost("/jobs/{id:guid}/run",Run);g.MapPost("/reminders/generate",GenerateReminders);return e;}
 private static async Task<IResult> Dashboard(LondonVIPDbContext db,ICompanyContext company,IAuditService audit,CancellationToken token)
 {
  var now=DateTimeOffset.UtcNow;var today=new DateTimeOffset(now.Date,TimeSpan.Zero);var jobs=db.WorkflowJobs.AsNoTracking().Where(x=>x.CompanyId==company.CompanyId);
  var counts=await jobs.GroupBy(x=>x.Status).Select(x=>new{x.Key,Count=x.Count()}).ToListAsync(token);int C(WorkflowJobStatus s)=>counts.FirstOrDefault(x=>x.Key==s)?.Count??0;
  var dto=new WorkflowDashboardDto{JobsWaiting=C(WorkflowJobStatus.Waiting)+C(WorkflowJobStatus.Scheduled),JobsCompleted=C(WorkflowJobStatus.Completed),JobsFailed=C(WorkflowJobStatus.Failed),RetryQueue=C(WorkflowJobStatus.Retrying),UpcomingReminders=await jobs.CountAsync(x=>x.WorkflowType.Contains("Reminder")&&x.ScheduledAt>=now&&x.Status!=WorkflowJobStatus.Cancelled,token),TodaysEvents=await db.BusinessEvents.CountAsync(x=>x.CompanyId==company.CompanyId&&x.OccurredAt>=today,token),AutomationsTriggered=await jobs.CountAsync(x=>x.CreatedAt>=today,token),Escalations=C(WorkflowJobStatus.Escalated),RecentJobs=await jobs.OrderByDescending(x=>x.CreatedAt).Take(10).Select(x=>new WorkflowJobDto(x.Id,x.WorkflowType,x.Kind,x.Status,x.ScheduledAt,x.AttemptCount,x.MaxAttempts,x.EscalationLevel,x.LastError,x.CorrelationId,x.CreatedAt)).ToListAsync(token)};
  await Audit(audit,company,"DashboardViewed","Workflow dashboard viewed.","Dashboard",token);return Results.Ok(dto);
 }
 private static Task<IResult> Jobs(WorkflowJobStatus? status,string? search,int? page,int? pageSize,IBackgroundJobService s,CancellationToken t)=>Ok(s.GetAsync(new(){Status=status,Search=search,Page=page??1,PageSize=pageSize??25},t));
 private static Task<IResult> History(IBackgroundJobService s,string? search,int? page,int? pageSize,CancellationToken t)=>Ok(s.GetAsync(new(){Status=WorkflowJobStatus.Completed,Search=search,Page=page??1,PageSize=pageSize??25},t));
 private static Task<IResult> Reminders(IBackgroundJobService s,string? search,int? page,int? pageSize,CancellationToken t)=>Ok(s.GetAsync(new(){Search=string.IsNullOrWhiteSpace(search)?"Reminder":search,Page=page??1,PageSize=pageSize??25},t));
 private static async Task<IResult> Events(LondonVIPDbContext db,ICompanyContext c,string? search,int? page,int? pageSize,CancellationToken t){var p=Math.Max(1,page??1);var size=Math.Clamp(pageSize??25,1,100);var q=db.BusinessEvents.AsNoTracking().Where(x=>x.CompanyId==c.CompanyId);if(!string.IsNullOrWhiteSpace(search))q=q.Where(x=>x.EventType.Contains(search)||x.ResourceType.Contains(search)||x.CorrelationId.Contains(search));var total=await q.CountAsync(t);var items=await q.OrderByDescending(x=>x.OccurredAt).Skip((p-1)*size).Take(size).Select(x=>new BusinessEventDto(x.Id,x.EventType,x.ResourceType,x.ResourceId,x.CorrelationId,x.OccurredAt)).ToListAsync(t);return Results.Ok(new WorkflowPageDto<BusinessEventDto>{Items=items,Page=p,PageSize=size,Total=total});}
 private static async Task<IResult> Rules(LondonVIPDbContext db,ICompanyContext c,CancellationToken t)=>Results.Ok(await db.WorkflowRules.AsNoTracking().Where(x=>x.CompanyId==c.CompanyId).OrderByDescending(x=>x.Priority).Select(x=>new{x.Id,x.Name,x.EventType,x.ConditionField,x.Operator,x.ComparisonValue,x.Action,x.Priority,x.IsActive}).ToListAsync(t));
 private static async Task<IResult> Retry(Guid id,IBackgroundJobService s,IAuditService a,ICompanyContext c,CancellationToken t){if(!await s.RetryAsync(id,t))return Results.NotFound();await Audit(a,c,"WorkflowRetried","Workflow job queued for retry.",id.ToString(),t);return Results.Ok();}
 private static async Task<IResult> Cancel(Guid id,IBackgroundJobService s,IAuditService a,ICompanyContext c,CancellationToken t){if(!await s.CancelAsync(id,t))return Results.NotFound();await Audit(a,c,"WorkflowCancelled","Workflow job cancelled.",id.ToString(),t);return Results.Ok();}
 private static async Task<IResult> Run(Guid id,IWorkflowEngine e,LondonVIPDbContext db,ICompanyContext c,IAuditService a,CancellationToken t){if(!await db.WorkflowJobs.AnyAsync(x=>x.Id==id&&x.CompanyId==c.CompanyId,t))return Results.NotFound();await e.ExecuteAsync(id,t);await Audit(a,c,"WorkflowRun","Workflow job manually executed.",id.ToString(),t);return Results.Ok();}
 private static async Task<IResult> GenerateReminders(IReminderService s,IAuditService a,ICompanyContext c,CancellationToken t){var count=await s.GenerateAsync(t);await Audit(a,c,"RemindersGenerated",$"Generated {count} reminders.","Reminders",t);return Results.Ok(new{count});}
 private static async Task<IResult> Ok<T>(Task<T> task)=>Results.Ok(await task);
 private static Task Audit(IAuditService a,ICompanyContext c,string type,string description,string resource,CancellationToken t)=>a.WriteAsync(type,"Workflows","Succeeded",SecurityEventSeverity.Information,description,"Workflow",resource,c.CompanyId,t);
}
