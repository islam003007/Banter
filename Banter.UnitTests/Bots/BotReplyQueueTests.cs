using Banter.Application.Abstractions.Bots;
using Banter.Infrastructure.Services.Bots;
using Banter.UnitTests.TestSupport;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Banter.UnitTests.Bots;

public class BotReplyQueueTests
{
    private static BotReplyRequest Request() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void TryEnqueue_WhenTheQueueIsFull_ReturnsFalseAndLogsAWarning()
    {
        var logger = new ListLogger<BotReplyQueue>();
        var queue = new BotReplyQueue(logger);

        for (var index = 0; index < 256; index++)
            Assert.True(queue.TryEnqueue(Request()));

        Assert.False(queue.TryEnqueue(Request()));

        var entry = Assert.Single(logger.Entries);

        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("full", entry.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryEnqueue_SucceedsAgainAfterTheWorkerDrainsAnItem()
    {
        var queue = new BotReplyQueue(new ListLogger<BotReplyQueue>());

        for (var index = 0; index < 256; index++)
            Assert.True(queue.TryEnqueue(Request()));

        Assert.False(queue.TryEnqueue(Request()));

        await using (var enumerator = queue.ReadAllAsync(CancellationToken.None).GetAsyncEnumerator())
        {
            Assert.True(await enumerator.MoveNextAsync());
        }

        Assert.True(queue.TryEnqueue(Request()));
    }
}
