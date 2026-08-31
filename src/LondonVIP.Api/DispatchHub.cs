using LondonVIP.Shared.Dispatch;
using LondonVIP.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LondonVIP.Api;

[Authorize(Policy = SecurityPolicies.DispatchOperations)]
public sealed class DispatchHub : Hub<IDispatchRealtimeClient>;
