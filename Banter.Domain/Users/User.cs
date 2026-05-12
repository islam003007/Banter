using Microsoft.AspNetCore.Identity;

namespace Banter.Domain.Users;

public class User : IdentityUser<Guid>, IAggregateRoot
{
    public string DisplayName { get; set; } = null!;
    public string? ProfilePictureUrl {  get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
