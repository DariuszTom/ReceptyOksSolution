namespace HomeSeeker.Models;

/// <summary>
/// Represents a user's search profile for real estate listings.
/// </summary>
public class SearchProfile
{
    public Guid Id { get; set; }

    /// <summary>
    /// Identity of the caller who created this profile (JWT <c>sub</c> claim).
    /// All API access to the profile is scoped to this owner. Null only for
    /// legacy rows created before ownership was introduced; such rows remain
    /// scannable by the background service but are not reachable via the API.
    /// </summary>
    public string? OwnerId { get; set; }

    /// <summary>
    /// City to search in (e.g., "warszawa", "krakow").
    /// </summary>
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// Optional district/neighborhood filter.
    /// </summary>
    public string? District { get; set; }

    /// <summary>
    /// Minimum price in PLN.
    /// </summary>
    public decimal? MinPrice { get; set; }

    /// <summary>
    /// Maximum price in PLN.
    /// </summary>
    public decimal? MaxPrice { get; set; }

    /// <summary>
    /// Minimum area in square meters.
    /// </summary>
    public decimal? MinAreaSqm { get; set; }

    /// <summary>
    /// Maximum area in square meters.
    /// </summary>
    public decimal? MaxAreaSqm { get; set; }

    /// <summary>
    /// Free-text criteria for the AI agent to consider during evaluation.
    /// </summary>
    public string? ExtraCriteria { get; set; }

    /// <summary>
    /// Email address to send scan reports to.
    /// </summary>
    public string NotificationEmail { get; set; } = string.Empty;

    /// <summary>
    /// Whether this profile is actively being scanned.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Timestamp of the last scan for this profile.
    /// Updated only when a scan completes (successfully or with failure),
    /// so a failed scan does not block retries for a full interval.
    /// </summary>
    public DateTime? LastScannedAt { get; set; }

    /// <summary>
    /// Short-lived lease held by whichever instance is currently running a scan
    /// for this profile. Prevents concurrent scans across replicas and lets
    /// on-demand triggers acquire the lock without being blocked by the
    /// "already scanned recently" check.
    /// </summary>
    public DateTime? ScanLeaseUntil { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Soft delete flag.
    /// </summary>
    public bool IsDeleted { get; set; }

    // Navigation properties
    public ICollection<HouseListing> Listings { get; set; } = new List<HouseListing>();
    public ICollection<ScanRun> ScanRuns { get; set; } = new List<ScanRun>();
}
