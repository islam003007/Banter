namespace Banter.Application.Abstractions.Realtime;

public interface INotificationService
{
    public Task SendMessageAsync(MessageNotification message, IReadOnlyList<string> userIds);
}
