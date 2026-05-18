using Banter.Domain.Users;

namespace Banter.Domain.Conversations;

public class ConversationParticipant // composite key so not a base intity
{
    public Guid UserId { get; private set; }
    public Guid ConversationId { get; private set; }
    public DateTime JoinedAt { get; } = DateTime.UtcNow;
    public Guid? LastSeenMessageId { get; set; }
    public Conversation Conversation { get; private set; } = null!;

    public User User { get; private set; } = null!;

    private ConversationParticipant()
    {

    }

    public ConversationParticipant(Guid userId)
    {
        UserId = userId;
    }
}