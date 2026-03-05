using Microsoft.Extensions.Logging;
using ReceptyOks.Data;

namespace ReceptyOks.Services;

/// <summary>
/// Background service that periodically deletes log entries older than 20 days.
/// Runs once on startup and then every 24 hours.
/// </summary>
public sealed class LogCleanupService : IDisposable
{
    private const int MaxLogAgeDays = 20;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly LocalDatabase _database;
    private readonly ILogger<LogCleanupService> _logger;
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;

    public LogCleanupService(LocalDatabase database, ILogger<LogCleanupService> logger)
    {
        _database = database;
        _logger = logger;
    }

    /// <summary>
    /// Starts the periodic cleanup loop. Call once at app startup.
    /// </summary>
    public async Task Start()
    {
        if (_cts is not null)
            return;

        _cts = new CancellationTokenSource();
        _timer = new PeriodicTimer(Interval);
        await RunAsync(_cts.Token);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        // Run immediately on startup, then on each timer tick.
        await CleanupAsync();

        try
        {
            while (await _timer!.WaitForNextTickAsync(ct))
            {
                await CleanupAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on disposal / app shutdown.
        }
    }

    private async Task CleanupAsync()
    {
        try
        {
            var deleted = await _database.ClearOldLogsAsync(MaxLogAgeDays);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Log cleanup failed");
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _timer?.Dispose();
        _timer = null;
    }
}
