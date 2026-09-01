namespace HomeSeeker.Models;

/// <summary>
/// Represents a scraped real estate listing.
/// </summary>
public class HouseListing
{
    public Guid Id { get; set; }

    /// <summary>
    /// Associated search profile ID.
    /// </summary>
    public Guid SearchProfileId { get; set; }

    /// <summary>
    /// Portal name (e.g., "Otodom", "OLX", "WebDiscovery").
    /// </summary>
    public string Portal { get; set; } = string.Empty;

    /// <summary>
    /// External ID from the portal (for deduplication).
    /// </summary>
    public string ExternalId { get; set; } = string.Empty;

    /// <summary>
    /// Full URL to the listing.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Listing title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Current price in PLN.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Previous price (if price dropped).
    /// </summary>
    public decimal? PreviousPrice { get; set; }

    /// <summary>
    /// Area in square meters.
    /// </summary>
    public decimal AreaSqm { get; set; }

    /// <summary>
    /// Location description.
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// When the listing was first discovered.
    /// </summary>
    public DateTime FirstSeenAt { get; set; }

    /// <summary>
    /// When the listing was last seen in a scan.
    /// </summary>
    public DateTime LastSeenAt { get; set; }

    /// <summary>
    /// AI evaluation score (0-100).
    /// </summary>
    public int? AiScore { get; set; }

    /// <summary>
    /// AI-generated summary of the listing.
    /// </summary>
    public string? AiSummary { get; set; }

    /// <summary>
    /// JSON array of pros identified by AI.
    /// </summary>
    public string? AiProsJson { get; set; }

    /// <summary>
    /// JSON array of cons identified by AI.
    /// </summary>
    public string? AiConsJson { get; set; }

    /// <summary>
    /// AI assessment of the price (e.g., "fair", "overpriced", "good deal").
    /// </summary>
    public string? AiPriceAssessment { get; set; }

    /// <summary>
    /// When the listing was evaluated by AI.
    /// </summary>
    public DateTime? EvaluatedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Soft delete flag.
    /// </summary>
    public bool IsDeleted { get; set; }

    // Navigation property
    public SearchProfile? SearchProfile { get; set; }
}
