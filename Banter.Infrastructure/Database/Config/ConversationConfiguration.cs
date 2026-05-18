using Banter.Domain.Constants;
using Banter.Domain.Conversations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banter.Infrastructure.Database.Config;

internal class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {

        builder.Property(c => c.Title).HasMaxLength(ConversationConstants.TitleMaxLength);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasMany(x => x.Participants)
            .WithOne(x => x.Conversation)
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
