using Banter.Application.Abstractions.Bots;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Banter.Infrastructure.Services.Bots;

/// <summary>
/// Consumes queued bot replies off the request thread. Each reply gets its own DI scope, so it has a
/// fresh DbContext and cannot hold a request's tracked state. A failure (model error, database
/// blip, cancellation) is logged and never crashes the API or stops the worker.
/// </summary>
internal sealed class BotReplyBackgroundService(
    IBotReplyQueue _queue,
    IServiceScopeFactory _scopeFactory,
    ILogger<BotReplyBackgroundService> _logger)
    : BackgroundService
{
    private const int MinimumDelayMs = 1200;
    private const int MaximumDelayMs = 1800;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var request in _queue.ReadAllAsync(stoppingToken))
            {
                var delay = Random.Shared.Next(MinimumDelayMs, MaximumDelayMs + 1);

                await Task.Delay(delay, stoppingToken);

                await ProcessRequestAsync(request, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
    }

    /// <summary>
    /// Processes one request and isolates its failure. Extracted from the loop so the isolation can
    /// be unit tested without waiting for the human-like typing delay.
    /// </summary>
    internal async Task ProcessRequestAsync(BotReplyRequest request, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            var processor = scope.ServiceProvider.GetRequiredService<IBotReplyProcessor>();

            await processor.ProcessAsync(request, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Bot reply failed for conversation {ConversationId} and bot {BotUserId}",
                request.ConversationId,
                request.BotUserId);
        }
    }
}
