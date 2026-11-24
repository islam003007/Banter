using Banter.Application.Features.Auth;
using Banter.Infrastructure.Database.DataSeed;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Banter.Infrastructure.Services.Demo;

/// <summary>
/// Resets the demo database on a fixed interval. Registration is conditional:
/// the service is only added when Demo:Enabled and Demo:Reset:Enabled are both on.
/// Never enable this against a database that contains real data.
/// </summary>
internal sealed class DemoResetService(
    IServiceProvider _serviceProvider,
    IOptions<DemoResetOptions> _options,
    ILogger<DemoResetService> _logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromHours(_options.Value.IntervalHours));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                _logger.LogWarning("Resetting demo data.");
                await SeederRunner.ResetDemoDataAsync(_serviceProvider);
                _logger.LogInformation("Demo data reset completed.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Demo data reset failed.");
            }
        }
    }
}
