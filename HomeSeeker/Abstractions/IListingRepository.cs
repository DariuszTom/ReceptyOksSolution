using HomeSeeker.Models;

namespace HomeSeeker.Abstractions;

/// <summary>
/// Repository interface for HomeSeeker data operations.
/// </summary>
public interface IListingRepository
{
    /// <summary>
    /// Gets the single household search profile, or null when it has not been
    /// configured yet. All users share this profile; the background scanner only
    /// ever scans this one.
    /// </summary>
    Task<SearchProfile?> GetSharedProfileAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or replaces the shared household profile with the supplied
    /// parameters. Changes take effect on the next periodic scan.
    /// <paramref name="updatedBy"/> records who last edited it.
    /// </summary>
    Task<SearchProfile> UpsertSharedProfileAsync(SearchProfileRequest request, string updatedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a search profile by ID. Intended for the background scan pipeline.
    /// </summary>
    Task<SearchProfile?> GetProfileByIdAsync(Guid profileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to acquire a scan lease for a periodic scan: succeeds only when the
    /// profile is active and due (<see cref="SearchProfile.LastScannedAt"/> older than
    /// <see cref="Configuration.HomeSeekerOptions.ScanInterval"/>) AND no active
    /// lease is held by another instance. On success the lease is held for
    /// a short, bounded duration and the returned token identifies this holder.
    /// Returns null otherwise.
    /// </summary>
    Task<Guid?> TryAcquireScanLeaseAsync(Guid profileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the scan lease identified by <paramref name="leaseToken"/>. When
    /// <paramref name="markScanned"/> is true the scan completed successfully and
    /// <see cref="SearchProfile.LastScannedAt"/> is stamped; otherwise only the lease
    /// is cleared so the profile is retried on the next cycle. If the lease has
    /// since been taken over by another holder nothing is changed and false is returned.
    /// </summary>
    Task<bool> ReleaseScanLeaseAsync(Guid profileId, Guid leaseToken, bool markScanned, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new scan run record.
    /// </summary>
    Task<ScanRun> CreateScanRunAsync(Guid profileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes a scan run with results.
    /// </summary>
    Task CompleteScanRunAsync(
        Guid scanRunId,
        ScanStatus status,
        int listingsFoundCount,
        int newListingsCount,
        int priceDropsCount,
        int evaluatedCount,
        string? reportHtml,
        string? error,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts a listing, tracking price changes and returning status.
    /// </summary>
    Task<UpsertResult> UpsertListingAsync(
        Guid profileId,
        ScrapedListing scraped,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes an already-stored listing that no longer matches the profile's criteria
    /// (current price, LastSeenAt) and hides it from listings and reports by soft-deleting it.
    /// Does nothing when the listing is not stored. A later <see cref="UpsertListingAsync"/>
    /// restores it once it matches again.
    /// </summary>
    Task ExcludeListingAsync(
        Guid profileId,
        ScrapedListing scraped,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the underlying change tracker. Must be called after an upsert
    /// failure so a tracked-but-not-saved entity does not poison every subsequent
    /// SaveChanges call in the same scope.
    /// </summary>
    void ResetTracking();

    /// <summary>
    /// Stores the listing's distance from the city centre.
    /// </summary>
    Task SaveDistanceAsync(
        Guid listingId,
        double distanceFromCenterKm,
        bool isApproximate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the listing's coordinates. Exact coordinates are never replaced by approximate ones.
    /// </summary>
    Task SaveCoordinatesAsync(
        Guid listingId,
        GeoPoint coordinates,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recomputes <see cref="HouseListing.DistanceFromCenterKm"/> from <paramref name="reference"/>
    /// for every active listing of the profile that has stored coordinates.
    /// </summary>
    /// <returns>Number of updated listings.</returns>
    Task<int> RecomputeDistancesAsync(
        Guid profileId,
        GeoPoint reference,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets scored active listings that have no stored coordinates yet (best score first),
    /// excluding <paramref name="excludeIds"/>.
    /// </summary>
    Task<IReadOnlyList<HouseListing>> GetListingsMissingCoordinatesAsync(
        Guid profileId,
        int count,
        IReadOnlyCollection<Guid> excludeIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets scored active listings with coordinates matching <paramref name="filter"/>,
    /// best score first (sort in the filter is ignored).
    /// </summary>
    Task<IReadOnlyList<HouseListing>> GetMapListingsAsync(
        Guid profileId,
        ListingQuery filter,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves AI evaluation results for a listing.
    /// </summary>
    Task SaveEvaluationAsync(
        Guid listingId,
        ListingEvaluation evaluation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets listings of a profile seen in any scan since <paramref name="seenSince"/>, used as the
    /// market sample for price medians. Only <c>Id</c>, <c>PropertyType</c>, <c>Price</c>,
    /// <c>AreaSqm</c> and <c>Location</c> are populated.
    /// </summary>
    Task<IReadOnlyList<HouseListing>> GetMarketSampleAsync(
        Guid profileId,
        DateTime seenSince,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active listings of a profile with only the fields needed to group units of one
    /// development / cross-portal copies: <c>Id</c>, <c>PropertyType</c>, <c>Price</c>, <c>AreaSqm</c>,
    /// <c>DevelopmentKey</c>, <c>AiScore</c> and the coordinates.
    /// </summary>
    Task<IReadOnlyList<HouseListing>> GetGroupingSampleAsync(
        Guid profileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets top-rated listings for a profile.
    /// </summary>
    Task<IReadOnlyList<HouseListing>> GetTopListingsAsync(
        Guid profileId,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets listings for a profile with pagination, filtering and sorting.
    /// </summary>
    Task<(IReadOnlyList<HouseListing> Listings, int TotalCount)> GetListingsAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        ListingQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets scan history for a profile.
    /// </summary>
    Task<IReadOnlyList<ScanRun>> GetScanHistoryAsync(
        Guid profileId,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a scan run by ID, including its HTML report, or null when it does not exist.
    /// </summary>
    Task<ScanRun?> GetScanRunByIdAsync(Guid scanRunId, CancellationToken cancellationToken = default);
}
