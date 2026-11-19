using System.Threading.Channels;
using Banter.Application.Abstractions.Bots;
using Microsoft.Extensions.Logging;

namespace Banter.Infrastructure.Services.Bots;

internal sealed class BotReplyQueue(ILogger<BotReplyQueue> _logger) : IBotReplyQueue
{
    private const int Capacity = 256;

    private readonly Channel<BotReplyRequest> _channel = Channel.CreateBounded<BotReplyRequest>(
        new BoundedChannelOptions(capacity: Capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    public bool TryEnqueue(BotReplyRequest request)
    {
        if (_channel.Writer.TryWrite(request))
            return true;

        // TryWrite is intentionally non-blocking: dropping the bot reply is better than slowing
        // down (or failing) the HTTP send that already persisted the human message. The queue is a
        // demo-friendly in-process accelerator, not a durable job store.
        _logger.LogWarning(
            "Bot reply queue is full ({Capacity}); dropping reply request for conversation {ConversationId}, bot {BotUserId}, triggering message {TriggeringMessageId}",
            Capacity,
            request.ConversationId,
            request.BotUserId,
            request.TriggeringMessageId);

        return false;
    }

    public IAsyncEnumerable<BotReplyRequest> ReadAllAsync(CancellationToken cancellationToken = default) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
