namespace Banter.Application.Abstractions.Ai;

/// <summary>
/// Generates the next bot message for a conversation. The default implementation is a deterministic
/// local demo brain; a real OpenAI-compatible implementation can be plugged in without touching the
/// bot reply pipeline.
/// </summary>
public interface IAiChatClient
{
    Task<string> GenerateReplyAsync(BotChatRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Everything a chat client needs to write the next bot turn. The history is ordered oldest to
/// newest and always ends with the human message that triggered the reply.
/// </summary>
public sealed record BotChatRequest(
    Guid ConversationId,
    Guid BotUserId,
    string BotEmail,
    string BotDisplayName,
    Guid HumanUserId,
    string HumanDisplayName,
    IReadOnlyList<BotChatMessage> History);

public sealed record BotChatMessage(
    Guid Id,
    Guid UserId,
    string SenderDisplayName,
    bool IsBot,
    string Content,
    DateTime CreatedAt);
