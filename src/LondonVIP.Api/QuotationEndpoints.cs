using System.Text.Json;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Security;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Pricing;
using LondonVIP.Shared.Quotations;
using LondonVIP.Shared.Security;
using LondonVIP.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LondonVIP.Api;

public static class QuotationEndpoints
{
    public static IEndpointRouteBuilder MapQuotationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group=endpoints.MapGroup("/api/quotations").RequireAuthorization(SecurityPolicies.BookingOperations).RequireRateLimiting("operations");
        group.MapGet("",ListAsync);group.MapGet("/{id:guid}",DetailAsync);group.MapPost("",CreateAsync);group.MapPut("/{id:guid}",UpdateAsync);
        group.MapPost("/{id:guid}/duplicate",DuplicateAsync);group.MapPost("/{id:guid}/convert",ConvertAsync);group.MapPost("/{id:guid}/cancel",CancelAsync);
        endpoints.MapGet("/api/customer-portal/{customerId:guid}/quotes",PortalListAsync).RequireAuthorization(SecurityPolicies.CustomerRead).RequireRateLimiting("operations");
        endpoints.MapPost("/api/customer-portal/{customerId:guid}/quotes/{id:guid}/accept",AcceptAsync).RequireAuthorization(SecurityPolicies.CustomerWrite).RequireRateLimiting("operations");
        endpoints.MapPost("/api/customer-portal/{customerId:guid}/quotes/{id:guid}/convert",PortalConvertAsync).RequireAuthorization(SecurityPolicies.CustomerWrite).RequireRateLimiting("operations");
        return endpoints;
    }

    private static async Task<IResult> ListAsync(LondonVIPDbContext db,ICompanyContext company,CancellationToken token)
    {var items=await Query(db,company.CompanyId).ToListAsync(token);return Results.Ok(items.Select(ToList).OrderByDescending(item=>item.PickupDateTime));}
    private static async Task<IResult> DetailAsync(Guid id,LondonVIPDbContext db,ICompanyContext company,CancellationToken token)
    {var item=await Query(db,company.CompanyId).SingleOrDefaultAsync(x=>x.Id==id,token);return item is null?Results.NotFound():Results.Ok(ToDetail(item));}
    private static async Task<IResult> CreateAsync(QuotationCreateDto request,IQuotationWorkflowService service,IAuditService audit,ICompanyContext company,CancellationToken token)
    {var result=await service.SaveAsync(request,token:token);if(result.Outcome==QuotationOperationOutcome.ValidationFailure)return Problem(result.Error);var quote=result.Quotation!;await audit.WriteAsync("QuotationCreated","Quotations","Succeeded",SecurityEventSeverity.Information,"Quotation created.","Quotation",quote.Id.ToString(),company.CompanyId,token);return Results.Created($"/api/quotations/{quote.Id}",ToDetail(quote));}
    private static async Task<IResult> UpdateAsync(Guid id,QuotationUpdateDto request,IQuotationWorkflowService service,IAuditService audit,ICompanyContext company,CancellationToken token)
    {var result=await service.SaveAsync(request,id,token);if(result.Outcome==QuotationOperationOutcome.NotFound)return Results.NotFound();if(result.Outcome==QuotationOperationOutcome.ValidationFailure)return Problem(result.Error);await audit.WriteAsync("QuotationUpdated","Quotations","Succeeded",SecurityEventSeverity.Information,"Draft quotation updated.","Quotation",id.ToString(),company.CompanyId,token);return Results.Ok(ToDetail(result.Quotation!));}
    private static async Task<IResult> DuplicateAsync(Guid id,LondonVIPDbContext db,IQuotationWorkflowService service,ICompanyContext company,IAuditService audit,CancellationToken token)
    {var source=await db.Quotations.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id&&x.CompanyId==company.CompanyId,token);if(source is null)return Results.NotFound();var request=ToCreate(source);request.SaveAsDraft=true;request.ExpiresAt=DateTimeOffset.UtcNow.AddHours(48);var result=await service.SaveAsync(request,token:token);if(result.Outcome!=QuotationOperationOutcome.Success)return Problem(result.Error);await audit.WriteAsync("QuotationDuplicated","Quotations","Succeeded",SecurityEventSeverity.Information,"Quotation duplicated as draft.","Quotation",result.Quotation!.Id.ToString(),company.CompanyId,token);return Results.Created($"/api/quotations/{result.Quotation.Id}",ToDetail(result.Quotation));}
    private static async Task<IResult> ConvertAsync(Guid id,IQuotationWorkflowService service,IAuditService audit,ICompanyContext company,CancellationToken token)
    {var result=await service.ConvertAsync(id,token);if(result.Outcome==QuotationOperationOutcome.NotFound)return Results.NotFound();if(result.Outcome==QuotationOperationOutcome.ValidationFailure)return Problem(result.Error);var already=result.Outcome==QuotationOperationOutcome.AlreadyConverted;await audit.WriteAsync(already?"QuotationConversionRepeated":"QuotationConverted","Quotations","Succeeded",SecurityEventSeverity.Information,already?"Existing converted booking returned.":"Quotation converted to booking.","Quotation",id.ToString(),company.CompanyId,token);var dto=new QuotationConversionDto(result.Booking!.Id,result.Booking.BookingReference,already);return already?Results.Ok(dto):Results.Created($"/api/bookings/{result.Booking.Id}",dto);}
    private static async Task<IResult> CancelAsync(Guid id,LondonVIPDbContext db,ICompanyContext company,IAuditService audit,CancellationToken token)
    {var quote=await db.Quotations.SingleOrDefaultAsync(x=>x.Id==id&&x.CompanyId==company.CompanyId,token);if(quote is null)return Results.NotFound();if(quote.Status==QuoteStatus.Converted)return Problem("Converted quotations cannot be cancelled.");quote.Status=QuoteStatus.Cancelled;quote.UpdatedAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync(token);await audit.WriteAsync("QuotationCancelled","Quotations","Succeeded",SecurityEventSeverity.Information,"Quotation cancelled.","Quotation",id.ToString(),company.CompanyId,token);return Results.Ok(ToDetail(quote));}

    private static async Task<IResult> PortalListAsync(Guid customerId,LondonVIPDbContext db,ICompanyContext company,CancellationToken token)
    {if(!await db.Customers.AnyAsync(x=>x.Id==customerId&&x.CompanyId==company.CompanyId,token))return Results.NotFound();var items=await Query(db,company.CompanyId).Where(x=>x.CustomerId==customerId).ToListAsync(token);return Results.Ok(items.Select(ToList).OrderByDescending(x=>x.PickupDateTime));}
    private static async Task<IResult> AcceptAsync(Guid customerId,Guid id,LondonVIPDbContext db,ICompanyContext company,IAuditService audit,CancellationToken token)
    {var quote=await db.Quotations.SingleOrDefaultAsync(x=>x.Id==id&&x.CustomerId==customerId&&x.CompanyId==company.CompanyId,token);if(quote is null)return Results.NotFound();if(EffectiveStatus(quote)!=QuoteStatus.Active)return Problem("Only an active, unexpired quotation can be accepted.");quote.AcceptedAt=DateTimeOffset.UtcNow;quote.UpdatedAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync(token);await audit.WriteAsync("QuotationAccepted","CustomerPortal","Succeeded",SecurityEventSeverity.Information,"Customer accepted quotation.","Quotation",id.ToString(),company.CompanyId,token);return Results.Ok(ToDetail(quote));}
    private static async Task<IResult> PortalConvertAsync(Guid customerId,Guid id,LondonVIPDbContext db,ICompanyContext company,IQuotationWorkflowService service,IAuditService audit,CancellationToken token)
    {var quote=await db.Quotations.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id&&x.CustomerId==customerId&&x.CompanyId==company.CompanyId,token);if(quote is null)return Results.NotFound();if(!quote.AcceptedAt.HasValue)return Problem("Accept the quotation before converting it to a booking.");return await ConvertAsync(id,service,audit,company,token);}

    private static IQueryable<Quotation> Query(LondonVIPDbContext db,Guid companyId)=>db.Quotations.AsNoTracking().Include(x=>x.Customer).Include(x=>x.CorporateAccount).Include(x=>x.Airport).Where(x=>x.CompanyId==companyId);
    private static QuoteStatus EffectiveStatus(Quotation q)=>q.Status==QuoteStatus.Active&&q.ExpiresAt<=DateTimeOffset.UtcNow?QuoteStatus.Expired:q.Status;
    private static QuotationListItemDto ToList(Quotation q)=>new(){Id=q.Id,QuoteReference=q.QuoteReference,CustomerName=q.Customer is null?string.Empty:$"{q.Customer.FirstName} {q.Customer.LastName}",CorporateAccountName=q.CorporateAccount?.AccountName,PickupDateTime=q.PickupDateTime,ExpiresAt=q.ExpiresAt,Status=EffectiveStatus(q),VehicleType=q.VehicleType,TotalFare=q.TotalFare,ConvertedBookingId=q.ConvertedBookingId};
    private static QuotationDetailDto ToDetail(Quotation q){var list=ToList(q);return new(){Id=list.Id,QuoteReference=list.QuoteReference,CustomerName=list.CustomerName,CorporateAccountName=list.CorporateAccountName,PickupDateTime=list.PickupDateTime,ExpiresAt=list.ExpiresAt,Status=list.Status,VehicleType=list.VehicleType,TotalFare=list.TotalFare,ConvertedBookingId=list.ConvertedBookingId,CustomerId=q.CustomerId,CorporateAccountId=q.CorporateAccountId,PickupAddress=q.PickupAddress,Destination=q.Destination,PassengerCount=q.PassengerCount,LuggageCount=q.LuggageCount,AirportId=q.AirportId,AirportCode=q.Airport?.Code,FlightNumber=q.FlightNumber,IsAirportPickup=q.IsAirportPickup,IsMeetAndGreet=q.IsMeetAndGreet,Notes=q.Notes,BaseFare=q.BaseFare,Extras=q.Extras,DiscountTotal=q.DiscountTotal,AcceptedAt=q.AcceptedAt,PricingBreakdown=JsonSerializer.Deserialize<List<PricingBreakdownItemDto>>(q.PricingBreakdownJson)??[]};}
    private static QuotationCreateDto ToCreate(Quotation q)=>new(){CustomerId=q.CustomerId,CorporateAccountId=q.CorporateAccountId,Pricing=JsonSerializer.Deserialize<QuoteRequest>(q.PricingRequestJson)??new(),PickupDateTime=q.PickupDateTime,FlightNumber=q.FlightNumber,Notes=q.Notes};
    private static IResult Problem(string? error)=>Results.ValidationProblem(new Dictionary<string,string[]>{{"quotation",[error??"Quotation operation failed."]}});
}
