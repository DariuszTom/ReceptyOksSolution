using HomeSeeker.Models;

namespace HomeSeeker.Abstractions;

/// <summary>
/// Repository interface for HomeSeeker data operations.
/// </summary>
public interface IListingRepository
{
    /// <summary>
    /// Gets all active search profiles that are due for scanning.
    /// </summary>
    Task<IReadOnlyList<SearchProfile>> GetActiveProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a search profile by ID.
    /// </summary>
    Task<SearchProfile?> GetProfileByIdAsync(Guid profileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to acquire a scan lease for a periodic scan: succeeds only when the
    /// profile is due (<see cref="SearchProfile.LastScannedAt"/> older than
    /// <see cref="Configuration.HomeSeekerOptions.ScanInterval"/>) AND no active
    /// lease is held by another instance. On success the lease is extended for
    /// a short, bounded duration. Returns false otherwise.
    /// </summary>
    Task<bool> TryMarkProfileScannedAsync(Guid profileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to acquire a scan lease for an on-demand scan: succeeds whenever no
    /// active lease is held, regardless of <see cref="SearchProfile.LastScannedAt"/>.
    /// This allows manual scans to run even shortly after a previous one.
    /// </summary>
    Task<bool> TryAcquireScanLeaseAsync(Guid profileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the scan lease and sets <see cref="SearchProfile.LastScannedAt"/>.
    /// Idempotent; safe to call even if the lease has already expired.
    /// </summary>
    Task ReleaseScanLeaseAsync(Guid profileId, CancellationToken cancellationToken = default);

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
    /// Gets a scan run by ID.
    /// </summary>
    Task<ScanRun?> GetScanRunByIdAsync(Guid scanRunId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new search profile.
    /// </summary>
    Task<SearchProfile> CreateProfileAsync(SearchProfileRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing search profile.
    /// </summary>
    Task<SearchProfile?> UpdateProfileAsync(Guid profileId, SearchProfileRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a search profile.
    /// </summary>
    Task<bool> DeleteProfileAsync(Guid profileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all search profiles (non-deleted).
    /// </summary>
    Task<IReadOnlyList<SearchProfile>> GetAllProfilesAsync(CancellationToken cancellationToken = default);
}
