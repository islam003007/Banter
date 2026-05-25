using Microsoft.AspNetCore.Identity;

namespace Banter.Domain.Users;

public class User : IdentityUser<Guid>
{
    public string DisplayName { get; private set; } = null!;
    public string? ProfilePictureUrl { get; set; }
    public DateTime CreatedAt { get; private init; } = DateTime.UtcNow;

    private User()
    {

    }

    public User(string email, string displayName, string? profilePictureUrl)
    {
        DisplayName = displayName;
        ProfilePictureUrl = profilePictureUrl;
        Email = email;
        UserName = email;
    }
}