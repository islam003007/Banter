using Banter.Application.Abstractions.Demo;
using Banter.Domain.Users;
using Banter.Infrastructure.Database.Seed;

namespace Banter.Infrastructure.Services;

/// <summary>
/// Demo users are exactly the users from the embedded seed data. Keeping this derived from
/// Users.json means the allow-list cannot drift from what is actually seeded.
/// </summary>
public sealed class SeededDemoUserDirectory : IDemoUserDirectory
{
    private static readonly IReadOnlyList<string> _emails = JsonDataLoader
        .LoadAsync<User>("Users.json")
        .Select(user => user.Email)
        .Where(email => !string.IsNullOrWhiteSpace(email))
        .Select(email => email!)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static readonly HashSet<string> _emailSet = new(_emails, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Emails => _emails;

    public bool IsAllowed(string? email) =>
        email is not null && _emailSet.Contains(email);
}
