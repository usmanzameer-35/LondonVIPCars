using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.CorporateAccounts;

public class CorporateAccountListItemDto
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string PrimaryContactName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public BillingTerms BillingTerms { get; set; }
    public decimal? CreditLimit { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; }
    public bool IsOnHold { get; set; }
    public DateTimeOffset? LastBookingDate { get; set; }
}
public sealed class CorporateAccountDetailDto : CorporateAccountListItemDto
{
    public string? TradingName { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? BillingEmail { get; set; }
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string TownCity { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public bool PurchaseOrderRequired { get; set; }
    public string? DefaultPurchaseOrderReference { get; set; }
    public string? Notes { get; set; }
    public int BookingCount { get; set; }
    public int UpcomingBookingCount { get; set; }
    public int CompletedBookingCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
public class CorporateAccountCreateDto
{
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? TradingName { get; set; }
    public string PrimaryContactName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? BillingEmail { get; set; }
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string TownCity { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public BillingTerms BillingTerms { get; set; }
    public decimal? CreditLimit { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsOnHold { get; set; }
    public bool PurchaseOrderRequired { get; set; }
    public string? DefaultPurchaseOrderReference { get; set; }
    public string? Notes { get; set; }
}
public sealed class CorporateAccountUpdateDto : CorporateAccountCreateDto;
public sealed class CorporateAccountStatusDto { public bool IsActive { get; set; } public bool IsOnHold { get; set; } }
public sealed class CorporateAccountSummaryDto { public int ActiveAccounts { get; set; } public int AccountsOnHold { get; set; } public int AccountsWithCreditLimit { get; set; } public int CorporateBookings { get; set; } public decimal CurrentBalanceTotal { get; set; } }
public sealed class CorporateAccountBookingDto { public Guid BookingId { get; set; } public string BookingReference { get; set; } = string.Empty; public DateTimeOffset PickupDateTime { get; set; } public string CustomerName { get; set; } = string.Empty; public string JourneySummary { get; set; } = string.Empty; public string? PurchaseOrderReference { get; set; } public decimal Amount { get; set; } public BookingStatus Status { get; set; } }
public sealed class CorporateAccountStatementDto { public Guid CorporateAccountId { get; set; } public string AccountNumber { get; set; } = string.Empty; public string AccountName { get; set; } = string.Empty; public DateTimeOffset StatementDate { get; set; } public decimal CurrentBalance { get; set; } public decimal? CreditLimit { get; set; } public decimal? AvailableCredit { get; set; } public bool IsOnHold { get; set; } public IReadOnlyList<CorporateAccountBookingDto> Transactions { get; set; } = []; }
