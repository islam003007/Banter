using Banter.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Banter.Infrastructure.Services.Realtime;

[Authorize]
public class ChatHub(IPresenceService _presenceService) : Hub<IChatClient>
{
    public override async Task OnConnectedAsync()
    {
        if (TryGetUserId(out var userId))
            await _presenceService.ConnectAsync(userId, Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (TryGetUserId(out var userId))
            await _presenceService.DisconnectAsync(userId, Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    public async Task Heartbeat()
    {
        if (TryGetUserId(out var userId))
            await _presenceService.HeartbeatAsync(userId, Context.ConnectionId);
    }

    private bool TryGetUserId(out Guid userId)
        => Guid.TryParse(Context.UserIdentifier, out userId);
}
