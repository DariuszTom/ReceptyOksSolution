using HomeSeeker.Configuration;
using Microsoft.Extensions.Options;
using ReceptyOks.Api.DbUtility;

namespace ReceptyOks.Api.Middleware;

/// <summary>
/// Periodically cleans stale HomeSeeker data: hides (soft-deletes) listings not seen for
/// <see cref="HomeSeekerOptions.ListingRetention"/> (only when their active profile kept being
/// scanned and their portal/property type was read successfully), hard-deletes hidden listings
/// unseen for <see cref="HomeSeekerOptions.ScanHistoryRetention"/>, and deletes scan runs
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
    public async Task<(int HiddenListings, int DeletedListings, int ScanRuns)> CleanupAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HomeSeekerDbContext>();

            var now = DateTime.UtcNow;
            var listingCutoff = now - _options.ListingRetention;
            var scanCutoff = now - _options.ScanHistoryRetention;

            // A listing is stale only when its own source (same portal and property type) was
            // read successfully within the window - evidenced by a sibling listing seen since the
            // cutoff. A profile-wide timestamp is not enough: a scan counts as successful when
            // one portal works while another is blocked. Paused profiles keep all their data.
            // Even then the offer may still be live beyond the scrapers' page cap, so it is only
            // hidden (soft-deleted, restored by the next upsert that finds it) and hard-deleted
            // once unseen for the much longer ScanHistoryRetention.
            var staleListings = db.HouseListings
                .Where(l => db.SearchProfiles.Any(p => p.Id == l.SearchProfileId
                        && p.IsActive
                        && p.LastScannedAt != null
                        && p.LastScannedAt > listingCutoff)
                    && db.HouseListings.Any(s => s.SearchProfileId == l.SearchProfileId
                        && s.Portal == l.Portal
                        && s.PropertyType == l.PropertyType
                        && s.LastSeenAt > listingCutoff));

            var deletedListings = await staleListings
                .Where(l => l.IsDeleted && l.LastSeenAt < scanCutoff)
                .ExecuteDeleteAsync(ct).ConfigureAwait(false);

            var hiddenListings = await staleListings
                .Where(l => !l.IsDeleted && l.LastSeenAt < listingCutoff)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(l => l.IsDeleted, true)
                    .SetProperty(l => l.UpdatedAt, now), ct)
                .ConfigureAwait(false);

            // A run still marked Running after the retention window is an abandoned one.
            var deletedScans = await db.ScanRuns
                .Where(s => s.StartedAt < scanCutoff)
                .ExecuteDeleteAsync(ct).ConfigureAwait(false);

            if ((hiddenListings > 0 || deletedListings > 0 || deletedScans > 0) && logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "HomeSeeker cleanup hid {Hidden} stale listings (unseen > {ListingDays} days), deleted {Listings} hidden listings and {Scans} scan runs (> {ScanDays} days)",
                    hiddenListings, _options.ListingRetention.Days, deletedListings, deletedScans, _options.ScanHistoryRetention.Days);
            }

            return (hiddenListings, deletedListings, deletedScans);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HomeSeeker cleanup failed");
            return (0, 0, 0);
        }
    }
}
