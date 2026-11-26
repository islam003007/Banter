using System.Runtime.CompilerServices;
using Banter.Application.Abstractions.Bots;

namespace Banter.Infrastructure.Services.Bots;

/// <summary>
/// Used when the bot feature is disabled. Keeping the queue registered lets the message
/// handler stay unchanged while guaranteeing that no reply is scheduled or processed.
/// </summary>
internal sealed class NoOpBotReplyQueue : IBotReplyQueue
{
    public bool TryEnqueue(BotReplyRequest request) => false;

    public async IAsyncEnumerable<BotReplyRequest> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }
}
