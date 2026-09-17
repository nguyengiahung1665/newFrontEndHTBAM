using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
namespace HTBAM.Api.Hubs;
[Authorize]
public sealed class SessionHub : Hub
{
    public Task JoinSession(long sessionId)=>Groups.AddToGroupAsync(Context.ConnectionId,$"session:{sessionId}");
    public Task LeaveSession(long sessionId)=>Groups.RemoveFromGroupAsync(Context.ConnectionId,$"session:{sessionId}");
}
