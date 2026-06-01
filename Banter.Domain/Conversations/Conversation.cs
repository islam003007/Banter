namespace Banter.Domain.Conversations;

public class Conversation : BaseEntity
{
    public string? Title { get; private init;}
    public DateTime CreatedAt { get; private init; } = DateTime.UtcNow;
    public bool IsGroup { get; private init; } = false;
    public Guid? LastMessageId { get; set; }
    public ICollection<ConversationParticipant> Participants { get; private set; } = new List<ConversationParticipant>();
    private Conversation()
    {

    }
    public static Conversation CreateDM(Guid firstUserId, Guid SecondUserId)
    {
        return new Conversation()
        {
            Title = null,
            IsGroup = false,
            Participants = new List<ConversationParticipant>()
            {
                new(firstUserId),
                new(SecondUserId)
            }
        };
    }

    public static Conversation CreateGroupConversation(IEnumerable<Guid> userIds, string title)
    {
        if (userIds.Distinct().Count() != userIds.Count())
            throw new InvalidOperationException();

        return new Conversation()
        {
            Title = title,
            IsGroup = true,
            Participants = userIds.Select(id => new ConversationParticipant(id)).ToList()
        };
    }
}
