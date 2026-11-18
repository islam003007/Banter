namespace Banter.Application.Abstractions.Bots;

/// <summary>
/// Processes one queued bot reply. Kept separate from the hosted service so the persistence and
/// realtime behaviour can be tested without waiting for the artificial typing delay.
/// </summary>
public interface IBotReplyProcessor
{
    Task ProcessAsync(BotReplyRequest request, CancellationToken cancellationToken = default);
}
