using Banter.Domain.Users;

namespace Banter.Domain.Conversations;

public class ConversationParticipant // composite key so not a base intity
{
    public Guid UserId { get; private init; }
    public Guid ConversationId { get; private init; }
    public DateTime JoinedAt { get; private init; } = DateTime.UtcNow;
    public Guid? LastSeenMessageId { get; set; }
    public Conversation Conversation { get; private init; } = null!;
    public User User { get; private set; } = null!;

    private ConversationParticipant()
    {

    }

    public ConversationParticipant(Guid userId)
    {
        UserId = userId;
    }
}