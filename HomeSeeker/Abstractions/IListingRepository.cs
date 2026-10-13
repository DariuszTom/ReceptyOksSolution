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
    /// lease is held by another instance. On success the lease is extended for
    /// a short, bounded duration. Returns false otherwise.
    /// </summary>
    Task<bool> TryMarkProfileScannedAsync(Guid profileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the scan lease. When <paramref name="markScanned"/> is true the scan
    /// completed successfully and <see cref="SearchProfile.LastScannedAt"/> is stamped;
    /// otherwise only the lease is cleared so the profile is retried on the next cycle.
    /// Idempotent; safe to call even if the lease has already expired.
    /// </summary>
    Task ReleaseScanLeaseAsync(Guid profileId, bool markScanned, CancellationToken cancellationToken = default);

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
    /// Resets the underlying change tracker. Must be called after an upsert
    /// failure so a tracked-but-not-saved entity does not poison every subsequent
    /// SaveChanges call in the same scope.
    /// </summary>
    void ResetTracking();

    /// <summary>
    /// Saves AI evaluation results for a listing.
    /// </summary>
    Task SaveEvaluationAsync(
        Guid listingId,
        ListingEvaluation evaluation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets top-rated listings for a profile.
    /// </summary>
    Task<IReadOnlyList<HouseListing>> GetTopListingsAsync(
        Guid profileId,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets listings for a profile with pagination and filtering.
    /// </summary>
    Task<(IReadOnlyList<HouseListing> Listings, int TotalCount)> GetListingsAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        int? minScore,
        string? sortBy,
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
