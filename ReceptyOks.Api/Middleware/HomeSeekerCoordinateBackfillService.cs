using HomeSeeker.Configuration;
using HomeSeeker.Services;
using Microsoft.Extensions.Options;

namespace ReceptyOks.Api.Middleware;

/// <summary>
/// Runs <see cref="CoordinateBackfill"/> in small batches after startup and then once per
/// <see cref="HomeSeekerOptions.CleanupInterval"/>, so scored listings saved before coordinates
/// were stored eventually appear on the map.
/// </summary>
public sealed class HomeSeekerCoordinateBackfillService(
    IServiceScopeFactory scopeFactory,
    IOptions<HomeSeekerOptions> options,
    ILogger<HomeSeekerCoordinateBackfillService> logger) : BackgroundService
{
    private const int BatchSize = 10;
    private static readonly TimeSpan BusyRetryDelay = TimeSpan.FromMinutes(5);

    private readonly HomeSeekerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
            return;

        try
        {
            // Start after the scan service's startup delay so the first scan gets the portals first.
            await Task.Delay(_options.StartupDelay + TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);

            await RunAsync(stoppingToken).ConfigureAwait(false);

            using PeriodicTimer timer = new(_options.CleanupInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await RunAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("HomeSeeker coordinate backfill is stopping");
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var tried = new HashSet<Guid>();
        while (!ct.IsCancellationRequested)
        {
            int located;
            bool more;
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var backfill = scope.ServiceProvider.GetRequiredService<CoordinateBackfill>();
                var before = tried.Count;
                (located, more) = await backfill.RunBatchAsync(BatchSize, tried, ct).ConfigureAwait(false);

                // Nothing was attempted although work remains: a scan is running.
                if (more && tried.Count == before)
                {
                    await Task.Delay(BusyRetryDelay, ct).ConfigureAwait(false);
                    continue;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "HomeSeeker coordinate backfill failed");
                return;
            }

            if (!more)
                return;

            await Task.Delay(_options.RequestDelay, ct).ConfigureAwait(false);
        }
    }
}
