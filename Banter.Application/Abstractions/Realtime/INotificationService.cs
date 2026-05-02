namespace Banter.Application.Abstractions.Realtime;

public interface INotificationService
{
    Task BroadcastMessageAsync(MessageNotification message, Guid ConversationId);
}

public record MessageNotification(Guid Id, string Content, Guid UserId, DateTime CreatedAt);