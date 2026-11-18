using Banter.Application.Abstractions.Realtime;

namespace Banter.Infrastructure.Services.Realtime;

public interface IChatClient
{
    Task ReceiveMessage(MessageNotification message);

    Task ReceiveBotTyping(Guid conversationId, string botName);
}
