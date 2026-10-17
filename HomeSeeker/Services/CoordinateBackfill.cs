using HomeSeeker.Abstractions;
using HomeSeeker.Configuration;
using HomeSeeker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HomeSeeker.Services;

/// <summary>
/// Fills in coordinates of scored listings saved before coordinates were persisted (or whose
/// search result had none), so they can be shown on the map. Fetches one ad page per listing,
/// politely spaced by <see cref="HomeSeekerOptions.RequestDelay"/>.
/// </summary>
public sealed class CoordinateBackfill(
    IEnumerable<IListingScraper> scrapers,
    IListingRepository repository,
    IOptions<HomeSeekerOptions> options,
    ILogger<CoordinateBackfill> logger,
    ICityCenterProvider? cityCenterProvider = null)
{
    private readonly HomeSeekerOptions _options = options.Value;

    /// <summary>
    /// Processes up to <paramref name="maxListings"/> listings. Stops early while a scan holds
    /// the lease (the scan itself saves coordinates and uses the same portals).
    /// </summary>
    /// <param name="skipIds">Listings already tried in this run (no coordinates on the ad page); extended in place.</param>
    /// <returns>Number of listings that got coordinates, and whether more work may remain.</returns>
    public async Task<(int Located, bool MoreRemaining)> RunBatchAsync(
        int maxListings, ISet<Guid> skipIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skipIds);

        var profile = await repository.GetSharedProfileAsync(cancellationToken).ConfigureAwait(false);
        if (profile is null || IsScanRunning(profile))
            return (0, profile is not null);

        var listings = await repository.GetListingsMissingCoordinatesAsync(
                profile.Id, maxListings, skipIds.ToList(), cancellationToken)
            .ConfigureAwait(false);
        if (listings.Count == 0)
            return (0, false);

        var cityCenter = await GetCityCenterAsync(profile.City, cancellationToken).ConfigureAwait(false);
        var reference = profile.Home ?? cityCenter;

        var located = 0;
        var requests = 0;
        foreach (var listing in listings)
        {
            skipIds.Add(listing.Id);

            var scraper = scrapers.FirstOrDefault(s =>
                string.Equals(s.PortalName, listing.Portal, StringComparison.OrdinalIgnoreCase));
            if (scraper is null)
                continue;

            if (requests++ > 0)
                await Task.Delay(_options.RequestDelay, cancellationToken).ConfigureAwait(false);

            var point = await FetchCoordinatesAsync(scraper, listing, cancellationToken).ConfigureAwait(false);
            if (point is not { IsValid: true } p ||
                (cityCenter is { } center && p.DistanceKmTo(center) > _options.MaxCoordinateDistanceFromCityKm))
            {
                continue;
            }

            await repository.SaveCoordinatesAsync(listing.Id, p, cancellationToken).ConfigureAwait(false);
            if (reference is { } r)
            {
                await repository.SaveDistanceAsync(
                        listing.Id, Math.Round(p.DistanceKmTo(r), 1), p.IsApproximate, cancellationToken)
                    .ConfigureAwait(false);
            }
            located++;
        }

        logger.LogInformation("Coordinate backfill located {Located} of {Count} listings", located, listings.Count);
        return (located, listings.Count == maxListings);
    }

    private static bool IsScanRunning(SearchProfile profile) =>
        profile.ScanLeaseUntil is { } until && until > DateTime.UtcNow;

    private async Task<GeoPoint?> FetchCoordinatesAsync(
        IListingScraper scraper, HouseListing listing, CancellationToken cancellationToken)
    {
        try
        {
            var details = await scraper.GetDetailsAsync(listing.Url, cancellationToken).ConfigureAwait(false);
            return details?.Coordinates;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Coordinate backfill failed for listing {ListingId}", listing.Id);
            return null;
        }
    }

    private async Task<GeoPoint?> GetCityCenterAsync(string city, CancellationToken cancellationToken)
    {
        if (cityCenterProvider is null || string.IsNullOrWhiteSpace(city))
            return null;

        try
        {
            return await cityCenterProvider.GetCenterAsync(city, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Resolving city centre failed for {City}", city);
            return null;
        }
    }
}
