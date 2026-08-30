using LondonVIP.Shared.Invoices;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Security;
using LondonVIP.Shared.Tenancy;
using LondonVIP.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using LondonVIP.Infrastructure.Data;

namespace LondonVIP.Api;

public static class InvoiceEndpoints
{
    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/invoices")
            .RequireAuthorization(SecurityPolicies.FinanceOperations)
            .RequireRateLimiting("operations");

        group.MapGet("", GetInvoicesAsync);
        group.MapGet("/summary", GetSummaryAsync);
        group.MapGet("/{id:guid}", GetInvoiceAsync);
        group.MapPost("", CreateInvoiceAsync);
        group.MapPut("/{id:guid}", UpdateInvoiceAsync);
        group.MapPatch("/{id:guid}/status", UpdateStatusAsync);
        group.MapPost("/{id:guid}/issue", IssueInvoiceAsync);
        group.MapPost("/{id:guid}/cancel", CancelInvoiceAsync);

        return endpoints;
    }

    private static async Task<IResult> GetInvoicesAsync(
        LondonVIPDbContext db,
        ICompanyContext company,
        int? status,
        Guid? customerId,
        Guid? accountId,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken)
    {
        var query = db.Invoices.AsNoTracking()
            .Where(invoice => invoice.CompanyId == company.CompanyId);

        if (status.HasValue)
            query = query.Where(invoice => (int)invoice.Status == status.Value);

        if (customerId.HasValue)
            query = query.Where(invoice => invoice.CustomerId == customerId.Value);

        if (accountId.HasValue)
            query = query.Where(invoice => invoice.CorporateAccountId == accountId.Value);

        if (fromDate.HasValue)
            query = query.Where(invoice => invoice.InvoiceDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(invoice => invoice.InvoiceDate <= toDate.Value);

        var invoices = await query
            .ToListAsync(cancellationToken);

        var sortedInvoices = invoices
            .OrderByDescending(invoice => invoice.InvoiceDate)
            .Select(invoice => new InvoiceListItemDto
            {
                Id = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                InvoiceDate = invoice.InvoiceDate,
                DueDate = invoice.DueDate,
                Status = invoice.Status.ToString(),
                CustomerOrAccountName = invoice.CorporateAccount != null
                    ? invoice.CorporateAccount.AccountName
                    : invoice.Customer != null
                    ? invoice.Customer.FirstName + " " + invoice.Customer.LastName
                    : "Unknown",
                TotalAmount = invoice.TotalAmount,
                AmountPaid = invoice.AmountPaid,
                BalanceDue = invoice.BalanceDue,
            })
            .ToList();

        return Results.Ok(sortedInvoices);
    }

    private static async Task<IResult> GetSummaryAsync(
        LondonVIPDbContext db,
        ICompanyContext company,
        CancellationToken cancellationToken)
    {
        var invoices = await db.Invoices.AsNoTracking()
            .Where(invoice => invoice.CompanyId == company.CompanyId)
            .ToListAsync(cancellationToken);

        var summary = new InvoiceSummaryDto
        {
            DraftInvoices = invoices.Count(i => i.Status == InvoiceStatus.Draft),
            OutstandingInvoices = invoices.Count(i => i.Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid),
            OverdueInvoices = invoices.Count(i => i.Status == InvoiceStatus.Overdue),
            PaidInvoices = invoices.Count(i => i.Status == InvoiceStatus.Paid),
            TotalOutstandingAmount = invoices
                .Where(i => i.Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid or InvoiceStatus.Overdue)
                .Sum(i => i.BalanceDue),
        };

        return Results.Ok(summary);
    }

    private static async Task<IResult> GetInvoiceAsync(
        Guid id,
        LondonVIPDbContext db,
        ICompanyContext company,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.AsNoTracking()
            .Include(i => i.Lines)
            .Include(i => i.Customer)
            .Include(i => i.CorporateAccount)
            .FirstOrDefaultAsync(i => i.Id == id && i.CompanyId == company.CompanyId, cancellationToken);

        if (invoice is null)
        {
            await AuditCrossTenantAsync(db, audit, id, company.CompanyId, "Invoice", cancellationToken);
            return Results.NotFound();
        }

        return Results.Ok(ToDetailDto(invoice));
    }

    private static async Task<IResult> CreateInvoiceAsync(
        InvoiceCreateDto request,
        LondonVIPDbContext db,
        ICompanyContext company,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var errors = InvoiceValidator.Validate(request, company.CompanyId, now);

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        // Verify customer/account ownership
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

        // Calculate invoice number (simple approach: INV-{company-prefix}-{sequential})
        var settings = await db.CompanySettings.SingleOrDefaultAsync(
            s => s.CompanyId == company.CompanyId, cancellationToken);
        var prefix = settings?.InvoicePrefix ?? "INV";

        var lastInvoice = await db.Invoices.AsNoTracking()
            .Where(i => i.CompanyId == company.CompanyId)
            .ToListAsync(cancellationToken);
        
        var lastInvoiceRecord = lastInvoice
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefault();

        var nextNumber = (lastInvoiceRecord is null ? 1 : 
            int.TryParse(lastInvoiceRecord.InvoiceNumber.Split('-').LastOrDefault(), out var num) ? num + 1 : 1);
        var invoiceNumber = $"{prefix}-{nextNumber:D6}";

        // Validate unique invoice number per company
        if (await db.Invoices.AnyAsync(i => i.CompanyId == company.CompanyId && i.InvoiceNumber == invoiceNumber, cancellationToken))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { "invoiceNumber", ["Invoice number already exists for your company."] }
            });

        // Get VAT rate from company settings
        var vatRate = settings?.VatEnabled == true ? settings.VatRate : 0m;

        // Create invoice with lines and calculate totals
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CompanyId = company.CompanyId,
            CorporateAccountId = request.CorporateAccountId,
            CustomerId = request.CustomerId,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = request.InvoiceDate ?? now,
            DueDate = request.DueDate ?? (request.InvoiceDate ?? now).AddDays(30),
            Status = InvoiceStatus.Draft,
            Notes = request.Notes,
            CreatedAt = now,
            UpdatedAt = now,
            Lines = []
        };

        foreach (var lineReq in request.Lines)
        {
            var quantity = lineReq.Quantity;
            var unitPrice = lineReq.UnitPrice;
            var taxRate = lineReq.TaxRate > 0 ? lineReq.TaxRate : vatRate;
            var lineSubtotal = quantity * unitPrice;
            var taxAmount = lineSubtotal * (taxRate / 100);
            var lineTotal = lineSubtotal + taxAmount;

            var line = new InvoiceLine
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoice.Id,
                BookingId = lineReq.BookingId,
                Description = lineReq.Description,
                Quantity = quantity,
                UnitPrice = unitPrice,
                TaxRate = taxRate,
                LineSubtotal = lineSubtotal,
                TaxAmount = taxAmount,
                LineTotal = lineTotal,
                CreatedAt = now
            };

            invoice.Lines.Add(line);
        }

        // Calculate invoice totals
        invoice.Subtotal = invoice.Lines.Sum(l => l.LineSubtotal);
        invoice.TaxAmount = invoice.Lines.Sum(l => l.TaxAmount);
        invoice.TotalAmount = invoice.Subtotal + invoice.TaxAmount;
        invoice.AmountPaid = 0;
        invoice.BalanceDue = invoice.TotalAmount;

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(
            "InvoiceCreated", "Invoice", "Succeeded", SecurityEventSeverity.Information,
            $"Invoice {invoiceNumber} created.",
            "Invoice", invoice.Id.ToString(), company.CompanyId, cancellationToken);

        return Results.Created($"/api/invoices/{invoice.Id}", ToDetailDto(invoice));
    }

    private static async Task<IResult> UpdateInvoiceAsync(
        Guid id,
        InvoiceUpdateDto request,
        LondonVIPDbContext db,
        ICompanyContext company,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id && i.CompanyId == company.CompanyId, cancellationToken);

        if (invoice is null)
            return Results.NotFound();

        // Only drafts can be updated
        if (invoice.Status != InvoiceStatus.Draft)
            return Results.BadRequest("Only draft invoices can be updated.");

        var now = DateTimeOffset.UtcNow;
        var errors = InvoiceValidator.Validate(request, company.CompanyId, now);

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        // Verify recipient ownership
        if (request.CustomerId.HasValue && request.CustomerId.Value != invoice.CustomerId)
        {
            var customer = await db.Customers.FirstOrDefaultAsync(
                c => c.Id == request.CustomerId.Value && c.CompanyId == company.CompanyId,
                cancellationToken);
            if (customer is null)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    { "customerId", ["Customer not found or not in your company."] }
                });
            invoice.CustomerId = request.CustomerId.Value;
        }

        if (request.CorporateAccountId.HasValue && request.CorporateAccountId.Value != invoice.CorporateAccountId)
        {
            var account = await db.CorporateAccounts.FirstOrDefaultAsync(
                a => a.Id == request.CorporateAccountId.Value && a.CompanyId == company.CompanyId,
                cancellationToken);
            if (account is null)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    { "corporateAccountId", ["Corporate account not found or not in your company."] }
                });
            invoice.CorporateAccountId = request.CorporateAccountId.Value;
        }

        invoice.InvoiceDate = request.InvoiceDate ?? invoice.InvoiceDate;
        invoice.DueDate = request.DueDate ?? invoice.DueDate;
        invoice.Notes = request.Notes;

        // Rebuild lines
        db.InvoiceLines.RemoveRange(invoice.Lines);
        invoice.Lines.Clear();

        var settings = await db.CompanySettings.SingleOrDefaultAsync(
            s => s.CompanyId == company.CompanyId, cancellationToken);
        var vatRate = settings?.VatEnabled == true ? settings.VatRate : 0m;

        foreach (var lineReq in request.Lines)
        {
            var quantity = lineReq.Quantity;
            var unitPrice = lineReq.UnitPrice;
            var taxRate = lineReq.TaxRate > 0 ? lineReq.TaxRate : vatRate;
            var lineSubtotal = quantity * unitPrice;
            var taxAmount = lineSubtotal * (taxRate / 100);
            var lineTotal = lineSubtotal + taxAmount;

            var line = new InvoiceLine
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoice.Id,
                BookingId = lineReq.BookingId,
                Description = lineReq.Description,
                Quantity = quantity,
                UnitPrice = unitPrice,
                TaxRate = taxRate,
                LineSubtotal = lineSubtotal,
                TaxAmount = taxAmount,
                LineTotal = lineTotal,
                CreatedAt = now
            };

            invoice.Lines.Add(line);
        }

        // Recalculate totals
        invoice.Subtotal = invoice.Lines.Sum(l => l.LineSubtotal);
        invoice.TaxAmount = invoice.Lines.Sum(l => l.TaxAmount);
        invoice.TotalAmount = invoice.Subtotal + invoice.TaxAmount;
        invoice.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(
            "InvoiceUpdated", "Invoice", "Succeeded", SecurityEventSeverity.Information,
            $"Invoice {invoice.InvoiceNumber} updated.",
            "Invoice", invoice.Id.ToString(), company.CompanyId, cancellationToken);

        return Results.Ok(ToDetailDto(invoice));
    }

    private static async Task<IResult> UpdateStatusAsync(
        Guid id,
        InvoiceStatusDto request,
        LondonVIPDbContext db,
        ICompanyContext company,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(
            i => i.Id == id && i.CompanyId == company.CompanyId, cancellationToken);

        if (invoice is null)
            return Results.NotFound();

        if (!Enum.TryParse<InvoiceStatus>(request.Status, true, out var newStatus))
            return Results.BadRequest("Invalid invoice status.");

        invoice.Status = newStatus;
        invoice.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(
            "InvoiceStatusUpdated", "Invoice", "Succeeded", SecurityEventSeverity.Information,
            $"Invoice {invoice.InvoiceNumber} status changed to {newStatus}.",
            "Invoice", invoice.Id.ToString(), company.CompanyId, cancellationToken);

        return Results.Ok(new { invoice.Id, invoice.Status });
    }

    private static async Task<IResult> IssueInvoiceAsync(
        Guid id,
        LondonVIPDbContext db,
        ICompanyContext company,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(
            i => i.Id == id && i.CompanyId == company.CompanyId, cancellationToken);

        if (invoice is null)
            return Results.NotFound();

        if (invoice.Status != InvoiceStatus.Draft)
            return Results.BadRequest("Only draft invoices can be issued.");

        invoice.Status = InvoiceStatus.Issued;
        invoice.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(
            "InvoiceIssued", "Invoice", "Succeeded", SecurityEventSeverity.Information,
            $"Invoice {invoice.InvoiceNumber} issued.",
            "Invoice", invoice.Id.ToString(), company.CompanyId, cancellationToken);

        return Results.Ok(new { invoice.Id, invoice.Status });
    }

    private static async Task<IResult> CancelInvoiceAsync(
        Guid id,
        LondonVIPDbContext db,
        ICompanyContext company,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(
            i => i.Id == id && i.CompanyId == company.CompanyId, cancellationToken);

        if (invoice is null)
            return Results.NotFound();

        if (invoice.Status == InvoiceStatus.Cancelled)
            return Results.BadRequest("Invoice is already cancelled.");

        if (invoice.Status == InvoiceStatus.Paid)
            return Results.BadRequest("Cannot cancel a fully paid invoice.");

        invoice.Status = InvoiceStatus.Cancelled;
        invoice.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(
            "InvoiceCancelled", "Invoice", "Succeeded", SecurityEventSeverity.Information,
            $"Invoice {invoice.InvoiceNumber} cancelled.",
            "Invoice", invoice.Id.ToString(), company.CompanyId, cancellationToken);

        return Results.Ok(new { invoice.Id, invoice.Status });
    }

    private static async Task AuditCrossTenantAsync(
        LondonVIPDbContext db,
        IAuditService audit,
        Guid id,
        Guid companyId,
        string resourceType,
        CancellationToken cancellationToken)
    {
        if (await db.Invoices.AnyAsync(i => i.Id == id && i.CompanyId != companyId, cancellationToken))
            await audit.WriteAsync(
                "CrossTenantAccessAttempt", "Authorization", "Denied", SecurityEventSeverity.High,
                $"Cross-tenant {resourceType} access was blocked.",
                resourceType, id.ToString(), companyId, cancellationToken);
    }

    private static InvoiceDetailDto ToDetailDto(Invoice invoice)
    {
        return new InvoiceDetailDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            Status = invoice.Status.ToString(),
            CorporateAccountId = invoice.CorporateAccountId,
            CorporateAccountName = invoice.CorporateAccount?.AccountName,
            CustomerId = invoice.CustomerId,
            CustomerName = invoice.Customer is not null
                ? $"{invoice.Customer.FirstName} {invoice.Customer.LastName}"
                : null,
            Subtotal = invoice.Subtotal,
            TaxAmount = invoice.TaxAmount,
            TotalAmount = invoice.TotalAmount,
            AmountPaid = invoice.AmountPaid,
            BalanceDue = invoice.BalanceDue,
            Notes = invoice.Notes,
            CreatedAt = invoice.CreatedAt,
            UpdatedAt = invoice.UpdatedAt,
            Lines = invoice.Lines.Select(line => new InvoiceLineDto
            {
                Id = line.Id,
                BookingId = line.BookingId,
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                TaxRate = line.TaxRate,
                LineSubtotal = line.LineSubtotal,
                TaxAmount = line.TaxAmount,
                LineTotal = line.LineTotal,
            }).ToList()
        };
    }
}
