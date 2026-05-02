using Banter.Domain.Conversations;
using Banter.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banter.Infrastructure.Database.Config;

internal class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {

        builder.Property(x => x.Title)
            .HasMaxLength(200);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasMany(x => x.Participants)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
