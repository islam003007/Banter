using Microsoft.AspNetCore.SignalR;

namespace Banter.Infrastructure.Services.Realtime;

public class ChatHub : Hub<IChatClient>
{
}
