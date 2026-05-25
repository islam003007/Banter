using Banter.Application.Abstractions.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace Banter.Infrastructure.Services.Realtime;

internal class SignalRNotifier(IHubContext<ChatHub, IChatClient> _hubcontext) : ISignalRNotifier
{
    public Task SendMessageAsync(MessageNotification message, IReadOnlyList<string> userIds)
    {
        return _hubcontext.Clients.Users(userIds).ReceiveMessage(message);
    }
}
