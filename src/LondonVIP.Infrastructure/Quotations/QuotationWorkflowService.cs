using System.Text.Json;
using LondonVIP.Infrastructure.Bookings;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Pricing;
using LondonVIP.Shared.Quotations;
using LondonVIP.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LondonVIP.Infrastructure.Quotations;

public sealed class QuotationWorkflowService(LondonVIPDbContext db, ICompanyContext company, IPricingService pricing) : IQuotationWorkflowService
{
    public async Task<QuotationOperationResult> SaveAsync(QuotationCreateDto request, Guid? quotationId = null, CancellationToken token = default)
    {
        var now = DateTimeOffset.UtcNow;
        var error = await ValidateAsync(request, token);
        if (error is not null) return new(QuotationOperationOutcome.ValidationFailure, Error: error);
        var quote = quotationId.HasValue
            ? await db.Quotations.SingleOrDefaultAsync(item => item.Id == quotationId && item.CompanyId == company.CompanyId, token)
            : null;
        if (quotationId.HasValue && quote is null) return new(QuotationOperationOutcome.NotFound);
        if (quote is not null && quote.Status != QuoteStatus.Draft) return new(QuotationOperationOutcome.ValidationFailure, Error: "Only draft quotations can be edited.");

        var calculation = await pricing.CalculateQuoteAsync(request.Pricing, token);
        if (!calculation.IsConfigured && !request.SaveAsDraft) return new(QuotationOperationOutcome.ValidationFailure, Error: calculation.Message ?? "Pricing is not configured.");
        quote ??= new Quotation { Id = Guid.NewGuid(), CompanyId = company.CompanyId, CreatedAt = now, QuoteReference = GenerateReference(now) };
        quote.CustomerId=request.CustomerId;quote.CorporateAccountId=request.CorporateAccountId;quote.PickupAddress=request.Pricing.PickupAddress.Trim();quote.Destination=request.Pricing.Destination.Trim();
        quote.PickupDateTime=request.PickupDateTime;quote.PassengerCount=request.Pricing.PassengerCount;quote.LuggageCount=request.Pricing.LuggageCount;quote.VehicleType=request.Pricing.VehicleType;
        quote.AirportId=request.Pricing.AirportId;quote.FlightNumber=Clean(request.FlightNumber)?.ToUpperInvariant();quote.IsAirportPickup=request.Pricing.IsAirportPickup;quote.IsMeetAndGreet=request.Pricing.IsMeetAndGreet;
        quote.Notes=Clean(request.Notes);quote.ExpiresAt=request.ExpiresAt??now.AddHours(48);quote.Status=request.SaveAsDraft?QuoteStatus.Draft:QuoteStatus.Active;
        quote.BaseFare=calculation.BaseFare;quote.Extras=calculation.TotalFare-calculation.BaseFare;quote.DiscountTotal=calculation.DiscountTotal;quote.TotalFare=calculation.TotalFare;
        quote.PricingBreakdownJson=JsonSerializer.Serialize(calculation.Breakdown);quote.PricingRequestJson=JsonSerializer.Serialize(request.Pricing);quote.UpdatedAt=now;
        if (!quotationId.HasValue) db.Quotations.Add(quote);
        await db.SaveChangesAsync(token);
        return new(QuotationOperationOutcome.Success, quote);
    }

    public async Task<QuotationOperationResult> ConvertAsync(Guid quotationId, CancellationToken token = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var quote = await db.Quotations.SingleOrDefaultAsync(item => item.Id == quotationId && item.CompanyId == company.CompanyId, token);
        if (quote is null) return new(QuotationOperationOutcome.NotFound);
        if (quote.ConvertedBookingId.HasValue)
        {
            var existing = await db.Bookings.SingleAsync(item => item.Id == quote.ConvertedBookingId && item.CompanyId == company.CompanyId, token);
            return new(QuotationOperationOutcome.AlreadyConverted, quote, existing);
        }
        var now=DateTimeOffset.UtcNow;
        if (quote.Status is QuoteStatus.Cancelled or QuoteStatus.Expired || quote.ExpiresAt <= now)
        {
            if (quote.Status == QuoteStatus.Active && quote.ExpiresAt <= now) { quote.Status=QuoteStatus.Expired;quote.UpdatedAt=now;await db.SaveChangesAsync(token);await transaction.CommitAsync(token); }
            return new(QuotationOperationOutcome.ValidationFailure, quote, Error:"Quotation is expired or cancelled.");
        }
        if (quote.Status != QuoteStatus.Active) return new(QuotationOperationOutcome.ValidationFailure, quote, Error:"Only active quotations can be converted.");
        var booking = new Booking
        {
            Id=Guid.NewGuid(),CompanyId=company.CompanyId,CustomerId=quote.CustomerId,CorporateAccountId=quote.CorporateAccountId,
            PickupAddress=quote.PickupAddress,Destination=quote.Destination,PickupDateTime=quote.PickupDateTime,PassengerCount=quote.PassengerCount,LuggageCount=quote.LuggageCount,
            VehicleType=quote.VehicleType,AirportId=quote.AirportId,FlightNumber=quote.FlightNumber,IsAirportPickup=quote.IsAirportPickup,IsMeetAndGreet=quote.IsMeetAndGreet,
            CustomerNotes=quote.Notes,BaseFare=quote.BaseFare,Extras=quote.Extras,TotalFare=quote.TotalFare,Status=BookingStatus.Pending,PaymentStatus="Pending",CreatedAt=now,UpdatedAt=now
        };
        booking.BookingReference=BookingReferenceGenerator.Generate(booking.Id,now);db.Bookings.Add(booking);
        quote.Status=QuoteStatus.Converted;quote.ConvertedBookingId=booking.Id;quote.ConvertedAt=now;quote.UpdatedAt=now;
        await db.SaveChangesAsync(token);await transaction.CommitAsync(token);
        return new(QuotationOperationOutcome.Success,quote,booking);
    }

    private async Task<string?> ValidateAsync(QuotationCreateDto request, CancellationToken token)
    {
        if (request.CustomerId==Guid.Empty)return "Customer is required.";
        if (!await db.Customers.AnyAsync(item=>item.Id==request.CustomerId&&item.CompanyId==company.CompanyId&&item.IsActive,token))return "Customer was not found for the current company.";
        if (request.CorporateAccountId is {} accountId && !await db.CorporateAccounts.AnyAsync(item=>item.Id==accountId&&item.CompanyId==company.CompanyId&&item.IsActive&&!item.IsOnHold,token))return "Corporate account was not found or is unavailable.";
        if(string.IsNullOrWhiteSpace(request.Pricing.PickupAddress)||string.IsNullOrWhiteSpace(request.Pricing.Destination))return "Pickup and destination are required.";
        if(request.PickupDateTime<=DateTimeOffset.UtcNow)return "Pickup date and time must be in the future.";
        if(request.ExpiresAt<=DateTimeOffset.UtcNow)return "Expiry must be in the future.";
        return null;
    }
    private static string GenerateReference(DateTimeOffset now)=>$"Q-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
    private static string? Clean(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
}
