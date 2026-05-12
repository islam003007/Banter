namespace Banter.Domain.Messages;

public class Message : BaseEntity, IAggregateRoot
{
    public Guid ConversationId { get; private set; }
    public Guid UserId { get; private set; }
    public string Content { get; private set; } = null!;
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
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
