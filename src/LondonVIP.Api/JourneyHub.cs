using System.Security.Claims;
using LondonVIP.Shared.Maps;
using LondonVIP.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LondonVIP.Api;

[Authorize(Policy = SecurityPolicies.ErpAccess)]
public sealed class JourneyHub : Hub<IJourneyRealtimeClient>
{
    public override async Task OnConnectedAsync()
    {
        var companyId = Context.User?.FindFirstValue("company_id");
        if (!string.IsNullOrWhiteSpace(companyId)) await Groups.AddToGroupAsync(Context.ConnectionId, $"company:{companyId}");
        await base.OnConnectedAsync();
    }
}
