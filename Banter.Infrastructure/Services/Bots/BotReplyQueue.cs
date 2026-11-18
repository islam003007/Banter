using System.Threading.Channels;
using Banter.Application.Abstractions.Bots;

namespace Banter.Infrastructure.Services.Bots;

internal sealed class BotReplyQueue : IBotReplyQueue
{
    private readonly Channel<BotReplyRequest> _channel = Channel.CreateBounded<BotReplyRequest>(
        new BoundedChannelOptions(capacity: 256)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    public bool TryEnqueue(BotReplyRequest request) => _channel.Writer.TryWrite(request);

    public IAsyncEnumerable<BotReplyRequest> ReadAllAsync(CancellationToken cancellationToken = default) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
