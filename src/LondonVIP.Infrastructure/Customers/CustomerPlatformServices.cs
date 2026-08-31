using System.Security.Claims;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Security;
using LondonVIP.Shared.CustomerPortal;
using LondonVIP.Shared.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LondonVIP.Infrastructure.Customers;

public sealed class CustomerIdentityResolver(IHttpContextAccessor http, LondonVIPDbContext db, ICompanyContext company) : ICustomerIdentityResolver
{
    public async Task<Guid?> GetCustomerIdAsync(CancellationToken token = default) { var raw = http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier); return Guid.TryParse(raw, out var id) ? await db.Users.AsNoTracking().Where(x => x.Id == id && x.CompanyId == company.CompanyId && x.IsActive).Select(x => x.CustomerId).SingleOrDefaultAsync(token) : null; }
}

public sealed class UnconfiguredPaymentGateway : IPaymentGateway
{
    public string ProviderName => "Unconfigured";
    public IReadOnlySet<string> SupportedMethods { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Card", "ApplePay", "GooglePay", "SavedCard" };
    public Task<CustomerPaymentIntentResult> CreateIntentAsync(CustomerPaymentIntentRequest request, CancellationToken token = default) => Task.FromResult(new CustomerPaymentIntentResult(false, null, "Unavailable", null, "Online payment provider is not configured."));
    public Task<CustomerPaymentIntentResult> RefundAsync(string paymentReference, decimal amount, CancellationToken token = default) => Task.FromResult(new CustomerPaymentIntentResult(false, null, "Unavailable", null, "Refund provider is not configured."));
}
