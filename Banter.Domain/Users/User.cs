using Microsoft.AspNetCore.Identity;

namespace Banter.Domain.Users;

public class User : IdentityUser<Guid>
{
    public string DisplayName { get; private set; } = null!;
    public string? ProfilePictureUrl { get; set; }

    /// <summary>
    /// Bot accounts are seeded (or created by an operator) with no password and can never sign in.
    /// They are regular users otherwise: searchable, addable to conversations and persisted as message senders.
    /// </summary>
    public bool IsBot { get; private init; }

    public DateTime CreatedAt { get; private init; } = DateTime.UtcNow;

    private User()
    {

    }

    public User(string email, string displayName, string? profilePictureUrl, bool isBot = false)
    {
        DisplayName = displayName;
        ProfilePictureUrl = profilePictureUrl;
        Email = email;
        UserName = email;
        IsBot = isBot;
    }
}