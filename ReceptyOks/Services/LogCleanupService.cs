using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReceptyOks.Data;

namespace ReceptyOks.Services;

/// <summary>
/// Background service that periodically deletes log entries older than 20 days.
/// Runs once on startup and then every 24 hours.
/// </summary>
public sealed class LogCleanupService(
    LocalDatabase database,
    ILogger<LogCleanupService> logger) : BackgroundService
{
    private const int MaxLogAgeDays = 7;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(12);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish loading before running non-critical cleanup.
        await Task.Delay(StartupDelay, stoppingToken);

        await CleanupAsync();

        using PeriodicTimer timer = new(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CleanupAsync();
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
            await database.ClearOldLogsAsync(MaxLogAgeDays);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Log cleanup failed");
        }
    }
}
