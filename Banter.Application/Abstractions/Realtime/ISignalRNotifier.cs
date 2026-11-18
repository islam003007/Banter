namespace Banter.Application.Abstractions.Realtime;

public interface ISignalRNotifier
{
    public Task SendMessageAsync(MessageNotification message, IReadOnlyList<string> userIds);

    /// <summary>
    /// Tells the given users that a bot is about to answer in a conversation.
    /// The client should clear this state when the next <c>ReceiveMessage</c> arrives or after a short timeout.
    /// </summary>
    public Task SendBotTypingAsync(Guid conversationId, string botName, IReadOnlyList<string> userIds);
}
