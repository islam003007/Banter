namespace Banter.Domain.Messages;

public class Message : BaseEntity
{
    public Guid ConversationId { get; private init; }
    public Guid UserId { get; private init; }
    public string Content { get; private set; } = null!;
    public DateTime CreatedAt { get; private init; } = DateTime.UtcNow;
    public bool IsEdited { get; private set; } = false;
    public DateTime? EditedAt { get; private set; }
    private Message()
    {

    }

    public Message(Guid conversationId, Guid userId, string content)
    {
        ConversationId = conversationId;
        UserId = userId;
        Content = content;
    }
}
