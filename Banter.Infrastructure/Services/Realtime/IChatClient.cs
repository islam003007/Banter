using Banter.Application.Abstractions.Realtime;

namespace Banter.Infrastructure.Services.Realtime;

public interface IChatClient
{
    Task ReeiveMessage(MessageNotification message);
}
