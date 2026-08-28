using System.Security.Claims;
using LondonVIP.Shared.Tenancy;
using Microsoft.AspNetCore.Http;

namespace LondonVIP.Infrastructure.Tenancy;

public sealed class AuthenticatedCompanyContext(IHttpContextAccessor accessor) : ICompanyContext
{
    public Guid CompanyId
    {
        get
        {
            var value = accessor.HttpContext?.User.FindFirstValue("company_id");
            return Guid.TryParse(value, out var companyId) ? companyId : LondonVipCompany.Id;
        }
    }
}
