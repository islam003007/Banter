using Banter.Application.Abstractions.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace Banter.Infrastructure.Services.Realtime;

internal class NotificationService(IHubContext<ChatHub, IChatClient> _hubcontext) : INotificationService
{
    public Task BroadcastMessageAsync(MessageNotification message, Guid ConversationId)
    {
        throw new NotImplementedException();
    }
}
