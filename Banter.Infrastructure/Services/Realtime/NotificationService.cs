using Banter.Application.Abstractions.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace Banter.Infrastructure.Services.Realtime;

internal class NotificationService(IHubContext<ChatHub, IChatClient> _hubcontext) : INotificationService
{
    public Task SendMessageAsync(MessageNotification message, IReadOnlyList<string> userIds)
    {
        return _hubcontext.Clients.Users(userIds).ReceiveMessage(message);
    }
}
