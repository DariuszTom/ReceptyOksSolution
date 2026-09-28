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
    /// Tries to mark a profile as scanned (for distributed locking).
    /// Returns false if another instance already started scanning.
    /// </summary>
    Task<bool> TryMarkProfileScannedAsync(Guid profileId, CancellationToken cancellationToken = default);

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
