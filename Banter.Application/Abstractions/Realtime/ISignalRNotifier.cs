namespace Banter.Application.Abstractions.Realtime;

public interface ISignalRNotifier
{
    public Task SendMessageAsync(MessageNotification message, IReadOnlyList<string> userIds);
}
