using Banter.Domain.Constants;
using Banter.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banter.Infrastructure.Database.Config;

internal class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(x => x.DisplayName)
            .HasMaxLength(UserConstants.DisplayNameMaxLength);

        builder.Property(x => x.ProfilePictureUrl)
            .HasMaxLength(UserConstants.ProfilePictureUrlMaxLength);
    }
}
