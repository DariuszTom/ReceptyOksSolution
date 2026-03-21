using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReceptyOks.Data;
using ReceptyOks.Shared.Configuration;

namespace ReceptyOks.Services;

/// <summary>
/// Background service that periodically deletes log entries older than the configured max age.
/// Runs once after a startup delay and then on a recurring interval.
/// </summary>
public sealed class LogCleanupService(
    LocalDatabase database,
    CleanupOptions options,
    ILogger<LogCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish loading before running non-critical cleanup.
        await Task.Delay(options.StartupDelay, stoppingToken).ConfigureAwait(false);

        await CleanupAsync().ConfigureAwait(false);

        using PeriodicTimer timer = new(options.Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CleanupAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Log cleanup service is stopping");
        }
    }

    private async Task CleanupAsync()
    {
        try
        {
            await database.ClearOldLogsAsync(options.MaxAge.Days).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Log cleanup failed");
        }
    }
}
