namespace Banter.Domain.Conversations;

public class Conversation : BaseEntity, IAggregateRoot
{
    public bool IsGroup { get; set; } // not necessery ?
    public string? Title { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();
}
