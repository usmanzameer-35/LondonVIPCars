using LondonVIP.Shared.Models;
using LondonVIP.Shared.Pricing;

namespace LondonVIP.Shared.Quotations;

public class QuotationCreateDto
{
    public Guid CustomerId { get; set; }
    public Guid? CorporateAccountId { get; set; }
    public QuoteRequest Pricing { get; set; } = new();
    public DateTimeOffset PickupDateTime { get; set; }
    public string? FlightNumber { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool SaveAsDraft { get; set; }
}

public sealed class QuotationUpdateDto : QuotationCreateDto;

public class QuotationListItemDto
{
    public Guid Id { get; set; }
    public string QuoteReference { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CorporateAccountName { get; set; }
    public DateTimeOffset PickupDateTime { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public QuoteStatus Status { get; set; }
    public VehicleType VehicleType { get; set; }
    public decimal TotalFare { get; set; }
    public Guid? ConvertedBookingId { get; set; }
}

public sealed class QuotationDetailDto : QuotationListItemDto
{
    public Guid CustomerId { get; set; }
    public Guid? CorporateAccountId { get; set; }
    public string PickupAddress { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public int PassengerCount { get; set; }
    public int LuggageCount { get; set; }
    public Guid? AirportId { get; set; }
    public string? AirportCode { get; set; }
    public string? FlightNumber { get; set; }
    public bool IsAirportPickup { get; set; }
    public bool IsMeetAndGreet { get; set; }
    public string? Notes { get; set; }
    public decimal BaseFare { get; set; }
    public decimal Extras { get; set; }
    public decimal DiscountTotal { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public List<PricingBreakdownItemDto> PricingBreakdown { get; set; } = [];
}

public sealed record QuotationConversionDto(Guid BookingId, string BookingReference, bool AlreadyConverted);

public enum QuotationOperationOutcome { Success, NotFound, ValidationFailure, AlreadyConverted }
public sealed record QuotationOperationResult(QuotationOperationOutcome Outcome, Quotation? Quotation = null, Booking? Booking = null, string? Error = null);

public interface IQuotationWorkflowService
{
    Task<QuotationOperationResult> SaveAsync(QuotationCreateDto request, Guid? quotationId = null, CancellationToken token = default);
    Task<QuotationOperationResult> ConvertAsync(Guid quotationId, CancellationToken token = default);
}
