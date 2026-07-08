namespace HomeSeeker.Models;

/// <summary>
/// API request DTO for creating/updating a search profile.
/// </summary>
public sealed record SearchProfileRequest
{
    /// <summary>
    /// City to search in (required).
    /// </summary>
    public required string City { get; init; }

    /// <summary>
    /// Optional district/neighborhood filter.
    /// </summary>
    public string? District { get; init; }

    /// <summary>
    /// Minimum price in PLN.
    /// </summary>
    public decimal? MinPrice { get; init; }

    /// <summary>
    /// Maximum price in PLN.
    /// </summary>
    public decimal? MaxPrice { get; init; }

    /// <summary>
    /// Minimum area in square meters.
    /// </summary>
    public decimal? MinAreaSqm { get; init; }

    /// <summary>
    /// Maximum area in square meters.
    /// </summary>
    public decimal? MaxAreaSqm { get; init; }

    /// <summary>
    /// Free-text criteria for AI evaluation.
    /// </summary>
    public string? ExtraCriteria { get; init; }

    /// <summary>
    /// Email address for notifications (required).
    /// </summary>
    public required string NotificationEmail { get; init; }

    /// <summary>
    /// Whether the profile should be active.
    /// </summary>
    public bool IsActive { get; init; } = true;
}
