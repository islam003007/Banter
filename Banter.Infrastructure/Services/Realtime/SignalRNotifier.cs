using Banter.Application.Abstractions.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace Banter.Infrastructure.Services.Realtime;

internal class SignalRNotifier(IHubContext<ChatHub, IChatClient> _hubcontext) : ISignalRNotifier
{
    public Task SendMessageAsync(
        MessageNotification message,
        IReadOnlyList<string> userIds,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return _hubcontext.Clients.Users(userIds).ReceiveMessage(message);
    }

    public Task SendBotTypingAsync(
        Guid conversationId,
        string botName,
        IReadOnlyList<string> userIds,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return _hubcontext.Clients.Users(userIds).ReceiveBotTyping(conversationId, botName);
    }
}
