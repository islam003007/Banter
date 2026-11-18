using Banter.Application.Abstractions.Ai;
using Banter.Application.Bots;

namespace Banter.Infrastructure.Services.Ai;

/// <summary>
/// Deterministic, self-contained demo brain. It needs no API key, network call or clock, so the
/// portfolio demo always answers in a predictable way. A real OpenAI-compatible client can replace
/// this registration later; the bot reply pipeline only depends on <see cref="IAiChatClient"/>.
/// </summary>
public sealed class DemoAiChatClient : IAiChatClient
{
    private static readonly string[] _genericReplies =
    [
        "I'm here. Give me a little more context and we can take the next step together.",
        "That makes sense. What outcome would count as a win for this change?",
        "Let's keep it small: one clear next step, then we can iterate."
    ];

    public Task<string> GenerateReplyAsync(BotChatRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var persona = BotPersonas.Find(request.BotEmail, request.BotDisplayName);

        var latest = request.History.LastOrDefault(message => !message.IsBot)
            ?? request.History.LastOrDefault();

        if (latest is null)
            return Task.FromResult(Fallback(request, persona));

        var content = latest.Content.Trim();

        if (IsGreeting(content))
            return Task.FromResult(Greeting(request, persona));

        var replies = persona is null
            ? _genericReplies
            : RepliesFor(persona);

        var index = StableHash($"{persona?.Email ?? request.BotEmail}\n{content}\n{request.History.Count}") % replies.Length;

        return Task.FromResult(replies[index].Replace("{name}", request.HumanDisplayName, StringComparison.Ordinal));
    }

    private static string Fallback(BotChatRequest request, BotPersona? persona)
    {
        var name = persona?.DisplayName ?? request.BotDisplayName;

        return $"I'm {name}, Banter's demo bot. Send me a question and I'll do my best to help.";
    }

    private static bool IsGreeting(string content)
    {
        var firstWord = content
            .Trim()
            .Split([' ', ',', '!', '.', '?'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()
            ?.ToLowerInvariant();

        return firstWord is "hi" or "hey" or "hello" or "yo" or "salam" or "salaam";
    }

    private static string Greeting(BotChatRequest request, BotPersona? persona) =>
        persona?.Email switch
        {
            "ada@bots.local" => $"Hey {request.HumanDisplayName}. What are we building today?",
            "milo@bots.local" => $"Hi {request.HumanDisplayName}. Describe what you're seeing and I'll help you narrow it down.",
            "nova@bots.local" => $"Hey {request.HumanDisplayName}! Give me a problem and I'll throw a few directions at it.",
            _ => $"Hi {request.HumanDisplayName}. What would you like to work on?"
        };

    private static string[] RepliesFor(BotPersona persona) => persona.Email switch
    {
        "ada@bots.local" => AdaReplies,
        "milo@bots.local" => MiloReplies,
        "nova@bots.local" => NovaReplies,
        _ => _genericReplies
    };

    private static readonly string[] AdaReplies =
    [
        "I'd separate scheduling from the write so a slow model can never block a send. A bounded queue plus an idempotency key on the triggering message gets you most of the way. What failure mode are you trying to rule out?",
        "That smells like a boundary problem: decide what owns retries before touching the happy path. One queue, one writer, and a message id that makes the write replay-safe. Want to sketch the state transitions?",
        "Start from the invariant you can't break. If it's \"one bot reply per human turn\", enforce it with the triggering message id at the write, not in the UI. Where would that check live for you?",
        "Keep the demo brain behind IAiChatClient so swapping in a real model is only a composition change. Give the worker a short timeout and fail closed. What latency budget do you have in mind?",
        "The cheapest reliability win is usually observability: log the trigger id, queue wait and reply duration. Then a missing reply is a query, not a mystery. What do you log today, {name}?",
        "Model the bot as a normal participant and a normal message row, with IsBot only for presentation. That keeps history, unread counts and pagination honest. Any reason to treat it differently?"
    ];

    private static readonly string[] MiloReplies =
    [
        "Let me restate it: a human message should produce exactly one bot reply, and you're seeing something else. Which part is wrong: the trigger, the queue, or the write? Start with one logged send and follow it.",
        "Before we change code, let's make the symptom boring. Send one message, log the trigger id, then confirm whether the bot wrote zero, one or two rows. Which is it, {name}?",
        "Useful next step: write the smallest failing test. Given one human message and a bot participant, assert exactly one bot message. Does that test fail today? That tells us where to look.",
        "Check the ordering first: is the bot seeing the message before it is committed? A queue entry that points at an uncommitted row can fail in a way that looks random. What order are save and enqueue in?",
        "Three hypotheses: the scope check skips the reply, the queue drops it, or the write collides. Add one log line at each boundary, then rerun. Which boundary is silent?",
        "Try to reproduce with a minimal conversation: one human, one bot, one message. If that works, the difference is in the history or the participants. What changes in the real case?"
    ];

    private static readonly string[] NovaReplies =
    [
        "Ooh, this could feel really alive! Three directions: a typing beat before the reply, a persona-specific opener, or a tiny \"thinking\" detail. Which one would make the demo feel most human?",
        "I love the one-to-one constraint. What if each bot had a signature move: Ada sketches a trade-off, Milo asks the sharp question, Nova offers three options. Want to make that explicit in the seed?",
        "Tiny idea with a big payoff: give each bot one memorable line it uses when it needs more context. It makes the demo feel designed instead of generated. Should we pick one per persona?",
        "What if the typing delay adapted to reply length? Short answer, quick beat; longer answer, a little more anticipation. It's a small touch that sells the illusion. Worth prototyping?",
        "There's a fun version where the bot reacts to what changed in the conversation, not just the last message. For the demo, a callback to the previous human line would be enough. Should we try that?",
        "Three ways to make this feel less canned: quote a phrase from the user, offer a concrete next step, and end with a question. Which one should we bake into all three personas?"
    ];

    private static int StableHash(string value)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        var hash = offsetBasis;

        foreach (var character in value)
        {
            hash ^= character;
            hash *= prime;
        }

        return (int)(hash & 0x7FFFFFFF);
    }
}
