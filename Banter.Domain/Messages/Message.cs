namespace Banter.Domain.Messages;

public class Message : BaseEntity, IAggregateRoot
{
    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }
    public string Content { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsEdited { get; set; } = false;
    public DateTime? EditedAt { get; set; }
}
