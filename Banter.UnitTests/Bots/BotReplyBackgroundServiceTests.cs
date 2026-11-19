using Banter.Application.Abstractions.Bots;
using Banter.Infrastructure.Services.Bots;
using Banter.UnitTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Banter.UnitTests.Bots;

public class BotReplyBackgroundServiceTests
{
    [Fact]
    public async Task ProcessRequestAsync_LogsAndSwallowsAFailureThenProcessesTheNextRequest()
    {
        var processor = new FailOnceProcessor();

        var services = new ServiceCollection();
        services.AddSingleton<IBotReplyProcessor>(processor);

        await using var provider = services.BuildServiceProvider();

        var logger = new ListLogger<BotReplyBackgroundService>();
        var service = new BotReplyBackgroundService(
            new EmptyBotReplyQueue(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            logger);

        var first = new BotReplyRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var second = new BotReplyRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        await service.ProcessRequestAsync(first, CancellationToken.None);
        await service.ProcessRequestAsync(second, CancellationToken.None);

        Assert.Equal(2, processor.Calls);
        Assert.Equal(1, processor.Succeeded);
        Assert.Equal(1, processor.Failures);

        var error = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, error.Level);
        Assert.Contains("Bot reply failed", error.Message);
    }

    private sealed class FailOnceProcessor : IBotReplyProcessor
    {
        public int Calls { get; private set; }
        public int Failures { get; private set; }
        public int Succeeded { get; private set; }

        public Task ProcessAsync(BotReplyRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;

            if (Calls == 1)
            {
                Failures++;
                throw new InvalidOperationException("Simulated model failure.");
            }

            Succeeded++;

            return Task.CompletedTask;
        }
    }
}
