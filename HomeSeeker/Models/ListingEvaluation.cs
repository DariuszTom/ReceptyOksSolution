namespace HomeSeeker.Models;

/// <summary>
/// AI evaluation result for a listing (DTO for ChatAsync&lt;T&gt;).
/// </summary>
public sealed record ListingEvaluation
{
    /// <summary>
    /// Overall score (0-100).
    /// </summary>
    public int Score { get; init; }

    /// <summary>
    /// Brief summary of the listing.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// List of pros/advantages.
    /// </summary>
    public List<string> Pros { get; init; } = [];

    /// <summary>
    /// List of cons/disadvantages.
    /// </summary>
    public List<string> Cons { get; init; } = [];

    /// <summary>
    /// Price assessment (e.g., "fair", "overpriced", "good deal", "below market").
    /// </summary>
    public string? PriceAssessment { get; init; }
}
