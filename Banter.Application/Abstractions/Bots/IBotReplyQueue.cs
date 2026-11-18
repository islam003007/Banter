namespace Banter.Application.Abstractions.Bots;

/// <summary>
/// A request to answer a human message with a bot reply. The reply is processed outside the HTTP
/// request so a slow model can never hold up <c>POST /conversations/{id}/messages</c>.
/// </summary>
public sealed record BotReplyRequest(
    Guid ConversationId,
    Guid BotUserId,
    Guid HumanUserId,
    Guid TriggeringMessageId);

/// <summary>
/// In-process queue for bot replies. The API only enqueues; a hosted service consumes.
/// </summary>
public interface IBotReplyQueue
{
    bool TryEnqueue(BotReplyRequest request);

    IAsyncEnumerable<BotReplyRequest> ReadAllAsync(CancellationToken cancellationToken = default);
}
