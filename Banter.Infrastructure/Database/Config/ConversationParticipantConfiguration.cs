using Banter.Domain.Conversations;
using Banter.Domain.Messages;
using Banter.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banter.Infrastructure.Database.Config;

internal class ConversationParticipantConfiguration : IEntityTypeConfiguration<ConversationParticipant>
{
    public void Configure(EntityTypeBuilder<ConversationParticipant> builder)
    {
        builder.HasKey(x => new { x.ConversationId, x.UserId });

        builder.HasOne<User>(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Message>()
            .WithMany()
            .HasForeignKey(x => x.LastSeenMessageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
