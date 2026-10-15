using HomeSeeker.Configuration;
using Microsoft.Extensions.Options;
using ReceptyOks.Api.DbUtility;

namespace ReceptyOks.Api.Middleware;

/// <summary>
/// Periodically hard-deletes stale HomeSeeker data:
/// listings not seen for <see cref="HomeSeekerOptions.ListingRetention"/> (only when their
/// profile kept being scanned, so they really disappeared from the portals) and scan runs
/// older than <see cref="HomeSeekerOptions.ScanHistoryRetention"/>.
/// </summary>
public sealed class HomeSeekerCleaner(
    IServiceScopeFactory scopeFactory,
    IOptions<HomeSeekerOptions> options,
    ILogger<HomeSeekerCleaner> logger) : BackgroundService
{
    private readonly HomeSeekerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(_options.StartupDelay, stoppingToken).ConfigureAwait(false);

            await CleanupAsync(stoppingToken).ConfigureAwait(false);

            using PeriodicTimer timer = new(_options.CleanupInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await CleanupAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("HomeSeeker cleanup service is stopping");
        }
    }

    /// <summary>
    /// Runs a single cleanup pass. Never throws except on cancellation.
    /// </summary>
    public async Task<(int Listings, int ScanRuns)> CleanupAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HomeSeekerDbContext>();

            var now = DateTime.UtcNow;
            var listingCutoff = now - _options.ListingRetention;
            var scanCutoff = now - _options.ScanHistoryRetention;

            var deletedListings = await db.HouseListings
                .Where(l => l.LastSeenAt < listingCutoff
                    && db.SearchProfiles.Any(p => p.Id == l.SearchProfileId
                        && p.LastScannedAt != null
                        && p.LastScannedAt > listingCutoff))
                .ExecuteDeleteAsync(ct).ConfigureAwait(false);

            // A run still marked Running after the retention window is an abandoned one.
            var deletedScans = await db.ScanRuns
                .Where(s => s.StartedAt < scanCutoff)
                .ExecuteDeleteAsync(ct).ConfigureAwait(false);

            if ((deletedListings > 0 || deletedScans > 0) && logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "HomeSeeker cleanup deleted {Listings} stale listings (unseen > {ListingDays} days) and {Scans} scan runs (> {ScanDays} days)",
                    deletedListings, _options.ListingRetention.Days, deletedScans, _options.ScanHistoryRetention.Days);
            }

            return (deletedListings, deletedScans);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HomeSeeker cleanup failed");
            return (0, 0);
        }
    }
}
