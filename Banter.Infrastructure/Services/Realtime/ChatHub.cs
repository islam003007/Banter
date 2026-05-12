using Banter.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Banter.Infrastructure.Services.Realtime;

[Authorize]
public class ChatHub(IPresenceService _presenceService) : Hub<IChatClient>
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;

        if (userId != null)
        {
            await _presenceService.SetOnlineAsync(userId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;

        if (userId != null)
        {
            await _presenceService.SetOfflineAsync(userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task Heartbeat()
    {
        var userId = Context.UserIdentifier;

        if (userId != null)
        {
            await _presenceService.SetOnlineAsync(userId);
        }
    }
}
