namespace HomeSeeker.Models;

/// <summary>
/// Result of upserting a listing (for tracking new/price changes).
/// </summary>
public sealed record UpsertResult
{
    /// <summary>
    /// The listing entity.
    /// </summary>
    public required HouseListing Listing { get; init; }

    /// <summary>
    /// Whether this is a newly discovered listing.
    /// </summary>
    public bool IsNew { get; init; }

    /// <summary>
    /// Whether the price dropped since last seen.
    /// </summary>
    public bool PriceDropped { get; init; }
}
