using System.Globalization;
using System.Security.Claims;
using ChonRieng.Api.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChonRieng.Api.Hubs;

[Authorize]
public sealed class PosHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var branchClaim = Context.User?.FindFirst("branch_id")?.Value;
        if (int.TryParse(branchClaim, NumberStyles.None, CultureInfo.InvariantCulture, out var branchId)
            && branchId > 0)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, PaymentGroups.Branch(branchId));
        }

        if (Context.User?.IsInRole("Manager") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, PaymentGroups.Managers);
        }

        await base.OnConnectedAsync();
    }
}
