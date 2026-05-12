namespace Banter.Domain.Conversations;

public class ConversationParticipant // composite key so not a base intity
{
    public Guid UserId { get; set; }
    public Guid ConversationId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public Guid? LastMessageId { get; set; }
    public Conversation Conversation { get; set; } = null!;
}
