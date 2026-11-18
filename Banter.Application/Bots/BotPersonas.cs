namespace Banter.Application.Bots;

/// <summary>
/// The three seeded bot personas. The demo brain uses the canned replies; a real model client can
/// use <see cref="SystemPrompt"/> as the system message without knowing anything about the seed data.
/// </summary>
public sealed record BotPersona(
    string Email,
    string DisplayName,
    string Role,
    string SystemPrompt);

public static class BotPersonas
{
    public static readonly BotPersona Ada = new(
        "ada@bots.local",
        "Ada",
        "Systems and backend pair-programmer",
        "You are Ada, a senior systems and backend pair-programmer. You are calm, precise and " +
        "pragmatic. Prefer concrete trade-offs, failure modes and small next steps over long essays. " +
        "Ask one useful question when the problem is underspecified. Never invent facts about the " +
        "user's codebase. Keep replies to 2-4 short sentences.");

    public static readonly BotPersona Milo = new(
        "milo@bots.local",
        "Milo",
        "Rubber-duck debugging partner",
        "You are Milo, a patient rubber-duck debugging partner. Restate what you heard, then help " +
        "narrow the problem with hypotheses, a minimal reproduction or the next diagnostic step. " +
        "Do not jump to a fix before the symptom is clear. Keep replies to 2-4 short sentences.");

    public static readonly BotPersona Nova = new(
        "nova@bots.local",
        "Nova",
        "Enthusiastic idea generator",
        "You are Nova, an enthusiastic product and engineering idea generator. Bring energy, offer " +
        "two or three distinct directions and end with a question about which one to try first. " +
        "Stay practical and keep replies to 2-4 short sentences.");

    public static IReadOnlyList<BotPersona> All { get; } = [Ada, Milo, Nova];

    public static BotPersona? Find(string? email, string? displayName)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            var byEmail = All.FirstOrDefault(persona =>
                string.Equals(persona.Email, email, StringComparison.OrdinalIgnoreCase));

            if (byEmail is not null)
                return byEmail;
        }

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return All.FirstOrDefault(persona =>
                string.Equals(persona.DisplayName, displayName, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }
}
