using Microsoft.AspNetCore.Identity;

namespace Banter.Domain.Users;

public class User : IdentityUser<Guid>, IAggregateRoot
{
    public string DisplayName { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
