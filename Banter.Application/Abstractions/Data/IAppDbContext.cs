using Banter.Domain.Conversations;
using Banter.Domain.Messages;
using Banter.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Banter.Application.Abstractions.Data;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Conversation> Conversations { get; }
    IQueryable<ConversationParticipant> ConversationParticipants { get; }
    DbSet<Message> Messages { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
