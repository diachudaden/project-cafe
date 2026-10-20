using ChonRieng.Application.Payments;
using Microsoft.AspNetCore.SignalR;

// INTEGRATE: namespace của PosHub trong dự án.
using ChonRieng.Api.Hubs;

namespace ChonRieng.Api.Payments;

/// <summary>
/// Đẩy sự kiện thanh toán về POS qua SignalR hub <c>/hubs/pos</c>.
/// </summary>
/// <remarks>
/// INTEGRATE: PosHub phải cho connection vào group khi kết nối, ví dụ:
/// <code>
/// public override async Task OnConnectedAsync()
/// {
///     var branchId = Context.User?.FindFirst("branch_id")?.Value;
///     if (branchId is not null) await Groups.AddToGroupAsync(Context.ConnectionId, PaymentGroups.Branch(int.Parse(branchId)));
///     if (Context.User?.IsInRole("Manager") == true) await Groups.AddToGroupAsync(Context.ConnectionId, PaymentGroups.Managers);
///     await base.OnConnectedAsync();
/// }
/// </code>
/// </remarks>
public sealed class SignalRPaymentNotifier(IHubContext<PosHub> hub) : IPaymentNotifier
{
    public Task PaymentReceivedAsync(int branchId, PaymentReceivedEvent evt, CancellationToken ct) =>
        hub.Clients.Group(PaymentGroups.Branch(branchId)).SendAsync(PaymentEvents.PaymentReceived, evt, ct);

    public Task SeatAssignedAsync(int branchId, SeatAssignedEvent evt, CancellationToken ct) =>
        hub.Clients.Group(PaymentGroups.Branch(branchId)).SendAsync(PaymentEvents.SeatAssigned, evt, ct);

    public Task NeedsAttentionAsync(int? branchId, PaymentNeedsAttentionEvent evt, CancellationToken ct)
    {
        var target = branchId is int id
            ? hub.Clients.Groups(PaymentGroups.Branch(id), PaymentGroups.Managers)
            : hub.Clients.Group(PaymentGroups.Managers);
        return target.SendAsync(PaymentEvents.PaymentNeedsAttention, evt, ct);
    }

    public Task SeatHoldExpiredAsync(int branchId, SeatHoldExpiredEvent evt, CancellationToken ct) =>
        hub.Clients.Group(PaymentGroups.Branch(branchId)).SendAsync(PaymentEvents.SeatHoldExpired, evt, ct);
}

public static class PaymentGroups
{
    public const string Managers = "managers";
    public static string Branch(int branchId) => $"branch-{branchId}";
}

/// <summary>Tên sự kiện — phải trùng với frontend (packages/api-client/src/payments.ts).</summary>
public static class PaymentEvents
{
    public const string PaymentReceived = "PaymentReceived";
    public const string SeatAssigned = "SeatAssigned";
    public const string PaymentNeedsAttention = "PaymentNeedsAttention";
    public const string SeatHoldExpired = "SeatHoldExpired";
}
