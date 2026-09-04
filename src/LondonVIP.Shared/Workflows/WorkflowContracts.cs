using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.Workflows;

public static class BusinessEventTypes
{
 public const string BookingCreated=nameof(BookingCreated),BookingAssigned=nameof(BookingAssigned),BookingAccepted=nameof(BookingAccepted),BookingDeclined=nameof(BookingDeclined),DriverArrived=nameof(DriverArrived),PassengerOnboard=nameof(PassengerOnboard),JourneyCompleted=nameof(JourneyCompleted),BookingCancelled=nameof(BookingCancelled),QuotationCreated=nameof(QuotationCreated),QuotationAccepted=nameof(QuotationAccepted),QuotationExpired=nameof(QuotationExpired),InvoiceCreated=nameof(InvoiceCreated),InvoiceDueSoon=nameof(InvoiceDueSoon),InvoiceOverdue=nameof(InvoiceOverdue),InvoicePaid=nameof(InvoicePaid),InvoiceRefunded=nameof(InvoiceRefunded),PaymentReceived=nameof(PaymentReceived),PaymentAllocated=nameof(PaymentAllocated),CreditNoteApproved=nameof(CreditNoteApproved),SupplierInvoiceApproved=nameof(SupplierInvoiceApproved),SupplierPaymentCreated=nameof(SupplierPaymentCreated),ExpenseApproved=nameof(ExpenseApproved),DriverSettlementCreated=nameof(DriverSettlementCreated),BankReconciled=nameof(BankReconciled),VatAdjusted=nameof(VatAdjusted),RecurringInvoiceGenerated=nameof(RecurringInvoiceGenerated),CustomerCreated=nameof(CustomerCreated),DriverCreated=nameof(DriverCreated),VehicleAdded=nameof(VehicleAdded),VehicleLicenceExpiring=nameof(VehicleLicenceExpiring),InsuranceExpiring=nameof(InsuranceExpiring),MOTExpiring=nameof(MOTExpiring),DriverLicenceExpiring=nameof(DriverLicenceExpiring),CorporateAccountCreated=nameof(CorporateAccountCreated),CorporateAccountOnHold=nameof(CorporateAccountOnHold),DriverOnline=nameof(DriverOnline),DriverOffline=nameof(DriverOffline),ShiftStarted=nameof(ShiftStarted),ShiftEnded=nameof(ShiftEnded),DriverBreakStarted=nameof(DriverBreakStarted),DriverBreakEnded=nameof(DriverBreakEnded),VehicleIssueReported=nameof(VehicleIssueReported);
}
public sealed record BusinessEvent(string EventType,string ResourceType,Guid? ResourceId,string PayloadJson,string? CorrelationId=null);
public sealed record ScheduleWorkflowRequest(string WorkflowType,string PayloadJson,DateTimeOffset ScheduledAt,WorkflowJobKind Kind=WorkflowJobKind.OneTime,string? CorrelationId=null,int MaxAttempts=3,string? Recurrence=null);
public sealed class WorkflowQuery { public WorkflowJobStatus? Status { get; set; } public string? Search { get; set; } public int Page { get; set; }=1; public int PageSize { get; set; }=25; }
public sealed class WorkflowPageDto<T>{public List<T> Items{get;set;}=[];public int Page{get;set;}public int PageSize{get;set;}public int Total{get;set;}}
public sealed record WorkflowJobDto(Guid Id,string WorkflowType,WorkflowJobKind Kind,WorkflowJobStatus Status,DateTimeOffset ScheduledAt,int AttemptCount,int MaxAttempts,int EscalationLevel,string? LastError,string CorrelationId,DateTimeOffset CreatedAt);
public sealed record BusinessEventDto(Guid Id,string EventType,string ResourceType,Guid? ResourceId,string CorrelationId,DateTimeOffset OccurredAt);
public sealed class WorkflowDashboardDto { public int JobsWaiting{get;set;}public int JobsCompleted{get;set;}public int JobsFailed{get;set;}public int RetryQueue{get;set;}public int UpcomingReminders{get;set;}public int TodaysEvents{get;set;}public int AutomationsTriggered{get;set;}public int Escalations{get;set;}public List<WorkflowJobDto> RecentJobs{get;set;}=[]; }
public sealed record WorkflowRuleContext(IReadOnlyDictionary<string,string> Values);
public sealed record WorkflowRuleResult(bool Matched,string? Action,string? RuleName);
public interface IWorkflowEngine { Task ExecuteAsync(Guid jobId,CancellationToken token=default); }
public interface IBackgroundJobService { Task<WorkflowJobDto> ScheduleAsync(ScheduleWorkflowRequest request,Guid? eventId=null,CancellationToken token=default);Task<WorkflowPageDto<WorkflowJobDto>> GetAsync(WorkflowQuery query,CancellationToken token=default);Task<bool> RetryAsync(Guid id,CancellationToken token=default);Task<bool> CancelAsync(Guid id,CancellationToken token=default); }
public interface IWorkflowScheduler { Task<WorkflowJobDto> ScheduleAsync(ScheduleWorkflowRequest request,CancellationToken token=default);Task ProcessDueAsync(CancellationToken token=default); }
public interface IBusinessEventPublisher { Task<Guid> PublishAsync(BusinessEvent businessEvent,CancellationToken token=default); }
public interface IBusinessEventHandler { Task HandleAsync(BusinessEventRecord businessEvent,CancellationToken token=default); }
public interface IRuleEngine { Task<WorkflowRuleResult> EvaluateAsync(string eventType,WorkflowRuleContext context,CancellationToken token=default); }
public interface IReminderService { Task<int> GenerateAsync(CancellationToken token=default); }
public interface IEscalationService { Task EscalateAsync(Guid jobId,string reason,CancellationToken token=default); }
