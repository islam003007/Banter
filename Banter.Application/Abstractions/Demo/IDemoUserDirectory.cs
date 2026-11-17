namespace Banter.Application.Abstractions.Demo;

/// <summary>
/// The allow-list of demo users. It is derived from the seeded users (Users.json) so the demo
/// endpoints can never sign in an account that was not intentionally prepared for the demo.
/// </summary>
public interface IDemoUserDirectory
{
    /// <summary>Seeded emails in seed order.</summary>
    IReadOnlyList<string> Emails { get; }

    bool IsAllowed(string? email);
}
