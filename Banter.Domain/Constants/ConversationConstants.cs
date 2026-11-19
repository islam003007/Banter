namespace Banter.Domain.Constants;

public static class ConversationConstants
{
    public const int TitleMaxLength = 200;

    /// <summary>
    /// Maximum number of participant ids accepted when creating a conversation, excluding the
    /// creator (who is added implicitly). Keeps group payloads and fan-out bounded.
    /// </summary>
    public const int MaxParticipants = 50;
}
