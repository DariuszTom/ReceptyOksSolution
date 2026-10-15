namespace HomeSeeker.Models;

/// <summary>
/// Represents the household's shared search profile for real estate listings.
/// There is exactly one non-deleted profile; every authenticated user reads and
/// edits the same one, and the background scanner scans only it.
/// </summary>
public class SearchProfile
{
    public Guid Id { get; set; }

    /// <summary>
    /// Identity of the caller who last edited this profile (JWT <c>sub</c> claim).
    /// Informational only; it does not restrict access.
    /// </summary>
    public string? OwnerId { get; set; }

    /// <summary>
    /// City to search in (e.g., "warszawa", "krakow").
    /// </summary>
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// Optional preferred district/neighborhood. This is a soft preference passed to
    /// the AI evaluator (affects scoring), not a portal search filter: the scrapers
    /// query by <see cref="City"/>, price and area only.
    /// </summary>
    public string? District { get; set; }

    /// <summary>
    /// Property kinds to scan for (houses, apartments or both).
    /// </summary>
    public PropertyTypes PropertyTypes { get; set; } = PropertyTypes.House;

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
    /// for this profile. Prevents concurrent scans across replicas.
    /// </summary>
    public DateTime? ScanLeaseUntil { get; set; }

    /// <summary>
    /// Identifies the holder of the current lease. Releasing requires the same
    /// token, so an instance whose lease expired cannot clobber a newer holder.
    /// </summary>
    public Guid? ScanLeaseToken { get; set; }

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
