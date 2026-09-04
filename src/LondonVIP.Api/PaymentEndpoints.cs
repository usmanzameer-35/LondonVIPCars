using LondonVIP.Shared.Payments;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Security;
using LondonVIP.Shared.Tenancy;
using LondonVIP.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Shared.Notifications;
using LondonVIP.Shared.Workflows;

namespace LondonVIP.Api;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/payments")
            .RequireAuthorization(SecurityPolicies.FinanceOperations)
            .RequireRateLimiting("operations");

        group.MapGet("", GetPaymentsAsync);
        group.MapGet("/summary", GetSummaryAsync);
        group.MapGet("/{id:guid}", GetPaymentAsync);
        group.MapPost("", CreatePaymentAsync);
        group.MapPut("/{id:guid}", UpdatePaymentAsync);
        group.MapPost("/{id:guid}/allocate", AllocatePaymentAsync);
        group.MapDelete("/{id:guid}/allocations/{allocationId:guid}", ReverseAllocationAsync);

        return endpoints;
    }

    private static async Task<IResult> GetPaymentsAsync(
        LondonVIPDbContext db,
        ICompanyContext company,
        string? reference,
        string? method,
        Guid? customerId,
        Guid? accountId,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken)
    {
        var query = db.Payments.AsNoTracking()
            .Include(p => p.Allocations)
            .Where(payment => payment.CompanyId == company.CompanyId);

        if (!string.IsNullOrWhiteSpace(reference))
            query = query.Where(payment => payment.PaymentReference.Contains(reference));

        if (!string.IsNullOrWhiteSpace(method))
            query = query.Where(payment => payment.PaymentMethod.ToString() == method);

        if (customerId.HasValue)
            query = query.Where(payment => payment.CustomerId == customerId.Value);

        if (accountId.HasValue)
            query = query.Where(payment => payment.CorporateAccountId == accountId.Value);

        if (fromDate.HasValue)
            query = query.Where(payment => payment.PaymentDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(payment => payment.PaymentDate <= toDate.Value);

        var payments = await query
            .ToListAsync(cancellationToken);

        var sortedPayments = payments
            .OrderByDescending(payment => payment.PaymentDate)
            .Select(payment => new PaymentListItemDto
            {
                Id = payment.Id,
                PaymentReference = payment.PaymentReference,
                PaymentDate = payment.PaymentDate,
                CustomerOrAccountName = payment.CorporateAccount != null
                    ? payment.CorporateAccount.AccountName
                    : payment.Customer != null
                    ? payment.Customer.FirstName + " " + payment.Customer.LastName
                    : "Unknown",
                PaymentMethod = payment.PaymentMethod.ToString(),
                Amount = payment.Amount,
                AllocatedAmount = payment.Allocations.Sum(a => a.Amount),
                UnallocatedAmount = payment.Amount - payment.Allocations.Sum(a => a.Amount),
            })
            .ToList();

        return Results.Ok(sortedPayments);
    }

    private static async Task<IResult> GetSummaryAsync(
        LondonVIPDbContext db,
        ICompanyContext company,
        CancellationToken cancellationToken)
    {
        var payments = await db.Payments.AsNoTracking()
            .Include(p => p.Allocations)
            .Where(payment => payment.CompanyId == company.CompanyId)
            .ToListAsync(cancellationToken);

        var totalReceived = payments.Sum(p => p.Amount);
        var totalAllocated = payments.Sum(p => p.Allocations.Sum(a => a.Amount));

        var summary = new PaymentSummaryDto
        {
            PaymentsReceived = totalReceived,
            UnallocatedAmount = totalReceived - totalAllocated,
            AllocatedAmount = totalAllocated,
            PaymentCount = payments.Count,
        };

        return Results.Ok(summary);
    }

    private static async Task<IResult> GetPaymentAsync(
        Guid id,
        LondonVIPDbContext db,
        ICompanyContext company,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var payment = await db.Payments.AsNoTracking()
            .Include(p => p.Allocations)
            .Include(p => p.Customer)
            .Include(p => p.CorporateAccount)
            .FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == company.CompanyId, cancellationToken);

        if (payment is null)
        {
            await AuditCrossTenantAsync(db, audit, id, company.CompanyId, "Payment", cancellationToken);
            return Results.NotFound();
        }

        return Results.Ok(ToDetailDto(payment));
    }

    private static async Task<IResult> CreatePaymentAsync(
        PaymentCreateDto request,
        LondonVIPDbContext db,
        ICompanyContext company,
        IAuditService audit,
        INotificationService notifications,
        CancellationToken cancellationToken)
    {
        var errors = PaymentValidator.Validate(request);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        // Verify recipient ownership
        if (request.CustomerId.HasValue)
        {
            var customer = await db.Customers.FirstOrDefaultAsync(
                c => c.Id == request.CustomerId.Value && c.CompanyId == company.CompanyId,
                cancellationToken);
            if (customer is null)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    { "customerId", ["Customer not found or not in your company."] }
                });
        }

        if (request.CorporateAccountId.HasValue)
        {
            var account = await db.CorporateAccounts.FirstOrDefaultAsync(
                a => a.Id == request.CorporateAccountId.Value && a.CompanyId == company.CompanyId,
                cancellationToken);
            if (account is null)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    { "corporateAccountId", ["Corporate account not found or not in your company."] }
                });
        }

        var now = DateTimeOffset.UtcNow;
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            CompanyId = company.CompanyId,
            PaymentReference = request.PaymentReference,
            PaymentDate = request.PaymentDate ?? now,
            Amount = request.Amount,
            PaymentMethod = (PaymentMethod)Enum.Parse(typeof(PaymentMethod), request.PaymentMethod),
            Notes = request.Notes,
            CorporateAccountId = request.CorporateAccountId,
            CustomerId = request.CustomerId,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Payments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(
            "PaymentRecorded", "Payment", "Succeeded", SecurityEventSeverity.Information,
            $"Payment {payment.PaymentReference} recorded for {payment.Amount:C}.",
            "Payment", payment.Id.ToString(), company.CompanyId, cancellationToken);
        var recipient=request.CustomerId.HasValue?await db.Customers.Where(x=>x.Id==request.CustomerId).Select(x=>x.Email).SingleAsync(cancellationToken):request.CorporateAccountId?.ToString()??"finance";await notifications.QueueAsync(new(recipient,request.CustomerId.HasValue?NotificationRecipientType.Customer:NotificationRecipientType.CorporateAccount,NotificationType.PaymentReceived,"Payment received",$"Payment {payment.PaymentReference} for {payment.Amount:C} was recorded.","payment-received",CorrelationId:payment.Id.ToString()),cancellationToken);

        return Results.Created($"/api/payments/{payment.Id}", ToDetailDto(payment));
    }

    private static async Task<IResult> UpdatePaymentAsync(
        Guid id,
        PaymentUpdateDto request,
        LondonVIPDbContext db,
        ICompanyContext company,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var payment = await db.Payments.FirstOrDefaultAsync(
            p => p.Id == id && p.CompanyId == company.CompanyId, cancellationToken);

        if (payment is null)
            return Results.NotFound();

        var errors = PaymentValidator.Validate(request);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        if (request.CustomerId.HasValue && request.CustomerId.Value != payment.CustomerId)
        {
            var customer = await db.Customers.FirstOrDefaultAsync(
                c => c.Id == request.CustomerId.Value && c.CompanyId == company.CompanyId,
                cancellationToken);
            if (customer is null)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    { "customerId", ["Customer not found or not in your company."] }
                });
            payment.CustomerId = request.CustomerId.Value;
        }

        if (request.CorporateAccountId.HasValue && request.CorporateAccountId.Value != payment.CorporateAccountId)
        {
            var account = await db.CorporateAccounts.FirstOrDefaultAsync(
                a => a.Id == request.CorporateAccountId.Value && a.CompanyId == company.CompanyId,
                cancellationToken);
            if (account is null)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    { "corporateAccountId", ["Corporate account not found or not in your company."] }
                });
            payment.CorporateAccountId = request.CorporateAccountId.Value;
        }

        payment.PaymentReference = request.PaymentReference;
        payment.PaymentDate = request.PaymentDate ?? payment.PaymentDate;
        payment.Amount = request.Amount;
        payment.PaymentMethod = (PaymentMethod)Enum.Parse(typeof(PaymentMethod), request.PaymentMethod);
        payment.Notes = request.Notes;
        payment.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(
            "PaymentUpdated", "Payment", "Succeeded", SecurityEventSeverity.Information,
            $"Payment {payment.PaymentReference} updated.",
            "Payment", payment.Id.ToString(), company.CompanyId, cancellationToken);

        payment = await db.Payments.AsNoTracking()
            .Include(p => p.Allocations)
            .Include(p => p.Customer)
            .Include(p => p.CorporateAccount)
            .FirstAsync(p => p.Id == id, cancellationToken);

        return Results.Ok(ToDetailDto(payment));
    }

    private static async Task<IResult> AllocatePaymentAsync(
        Guid id,
        PaymentAllocationCreateDto request,
        LondonVIPDbContext db,
        ICompanyContext company,
        IAuditService audit,
        IBusinessEventPublisher events,
        CancellationToken cancellationToken)
    {
        var payment = await db.Payments.AsNoTracking()
            .Include(p => p.Allocations)
            .FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == company.CompanyId, cancellationToken);

        if (payment is null)
            return Results.NotFound();

        // Verify invoice ownership and validity
        var invoice = await db.Invoices
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId && i.CompanyId == company.CompanyId, cancellationToken);

        if (invoice is null)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { "invoiceId", ["Invoice not found or not in your company."] }
            });

        if (invoice.Status == InvoiceStatus.Cancelled)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { "invoiceId", ["Cannot allocate payment to a cancelled invoice."] }
            });

        // Validate allocation amount
        var allocatedAmount = payment.Allocations.Sum(a => a.Amount);
        var allocationErrors = PaymentValidator.ValidateAllocation(request, payment.Amount, allocatedAmount);
        if (allocationErrors.Count > 0)
            return Results.ValidationProblem(allocationErrors);

        // Check invoice balance
        if (request.Amount > invoice.BalanceDue)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { "amount", [$"Allocation amount cannot exceed invoice balance of {invoice.BalanceDue:C}."] }
            });

        // Create allocation
        var now = DateTimeOffset.UtcNow;
        var allocation = new PaymentAllocation
        {
            Id = Guid.NewGuid(),
            PaymentId = payment.Id,
            InvoiceId = invoice.Id,
            Amount = request.Amount,
            CreatedAt = now
        };

        db.PaymentAllocations.Add(allocation);

        // Update invoice totals
        invoice.AmountPaid += request.Amount;
        invoice.BalanceDue = invoice.TotalAmount - invoice.AmountPaid;

        // Update invoice status based on payment
        if (invoice.BalanceDue <= 0)
            invoice.Status = InvoiceStatus.Paid;
        else if (invoice.AmountPaid > 0)
            invoice.Status = InvoiceStatus.PartiallyPaid;

        invoice.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(
            "PaymentAllocated", "Payment", "Succeeded", SecurityEventSeverity.Information,
            $"Payment {payment.PaymentReference} allocated {request.Amount:C} to invoice {invoice.InvoiceNumber}.",
            "Payment", payment.Id.ToString(), company.CompanyId, cancellationToken);

        await events.PublishAsync(new(BusinessEventTypes.PaymentAllocated, "PaymentAllocation", allocation.Id, System.Text.Json.JsonSerializer.Serialize(new { request.Amount }), allocation.Id.ToString()), cancellationToken);
        if (invoice.Status == InvoiceStatus.Paid) await events.PublishAsync(new(BusinessEventTypes.InvoicePaid, "Invoice", invoice.Id, System.Text.Json.JsonSerializer.Serialize(new { invoice.TotalAmount }), invoice.Id.ToString()), cancellationToken);

        return Results.Created($"/api/payments/{payment.Id}/allocations/{allocation.Id}", 
            new { allocation.Id, allocation.Amount, invoice.Status });
    }

    private static async Task<IResult> ReverseAllocationAsync(
        Guid id,
        Guid allocationId,
        LondonVIPDbContext db,
        ICompanyContext company,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var payment = await db.Payments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == company.CompanyId, cancellationToken);

        if (payment is null)
            return Results.NotFound();

        var allocation = await db.PaymentAllocations
            .Include(a => a.Invoice)
            .FirstOrDefaultAsync(a => a.Id == allocationId && a.PaymentId == id, cancellationToken);

        if (allocation is null)
            return Results.NotFound();

        var invoice = allocation.Invoice;

        // Reverse the allocation
        invoice.AmountPaid -= allocation.Amount;
        invoice.BalanceDue = invoice.TotalAmount - invoice.AmountPaid;

        // Update invoice status
        if (invoice.AmountPaid <= 0)
            invoice.Status = InvoiceStatus.Issued;
        else if (invoice.AmountPaid < invoice.TotalAmount)
            invoice.Status = InvoiceStatus.PartiallyPaid;

        invoice.UpdatedAt = DateTimeOffset.UtcNow;

        db.PaymentAllocations.Remove(allocation);
        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(
            "PaymentAllocationReversed", "Payment", "Succeeded", SecurityEventSeverity.Information,
            $"Payment allocation {allocation.Amount:C} from invoice {invoice.InvoiceNumber} reversed.",
            "Payment", payment.Id.ToString(), company.CompanyId, cancellationToken);

        return Results.Ok(new { Message = "Allocation reversed." });
    }

    private static async Task AuditCrossTenantAsync(
        LondonVIPDbContext db,
        IAuditService audit,
        Guid id,
        Guid companyId,
        string resourceType,
        CancellationToken cancellationToken)
    {
        if (await db.Payments.AnyAsync(p => p.Id == id && p.CompanyId != companyId, cancellationToken))
            await audit.WriteAsync(
                "CrossTenantAccessAttempt", "Authorization", "Denied", SecurityEventSeverity.High,
                $"Cross-tenant {resourceType} access was blocked.",
                resourceType, id.ToString(), companyId, cancellationToken);
    }

    private static PaymentDetailDto ToDetailDto(Payment payment)
    {
        return new PaymentDetailDto
        {
            Id = payment.Id,
            PaymentReference = payment.PaymentReference,
            PaymentDate = payment.PaymentDate,
            CorporateAccountId = payment.CorporateAccountId,
            CorporateAccountName = payment.CorporateAccount?.AccountName,
            CustomerId = payment.CustomerId,
            CustomerName = payment.Customer is not null
                ? $"{payment.Customer.FirstName} {payment.Customer.LastName}"
                : null,
            PaymentMethod = payment.PaymentMethod.ToString(),
            Amount = payment.Amount,
            Notes = payment.Notes,
            CreatedAt = payment.CreatedAt,
            UpdatedAt = payment.UpdatedAt,
            Allocations = payment.Allocations.Select(a => new PaymentAllocationDto
            {
                Id = a.Id,
                InvoiceId = a.InvoiceId,
                InvoiceNumber = a.Invoice?.InvoiceNumber ?? string.Empty,
                Amount = a.Amount,
            }).ToList()
        };
    }
}
