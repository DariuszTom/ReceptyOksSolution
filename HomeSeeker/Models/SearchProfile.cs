namespace HomeSeeker.Models;

/// <summary>
/// Represents a user's search profile for real estate listings.
/// </summary>
public class SearchProfile
{
    public Guid Id { get; set; }

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
    /// </summary>
    public DateTime? LastScannedAt { get; set; }

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
