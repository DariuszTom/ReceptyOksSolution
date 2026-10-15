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
    /// Whether the buyer's hard requirements (e.g. "minimum 4 pokoje") are met:
    /// "spełnione", "niespełnione" or "brak danych". Used only to cap <see cref="Score"/>; not persisted.
    /// </summary>
    public string? Requirements { get; init; }

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
