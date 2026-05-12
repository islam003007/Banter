namespace Banter.Domain.Conversations;

public class Conversation : BaseEntity, IAggregateRoot
{
    public string? Title { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsGroup { get; set; } = false;
    public ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();
}
