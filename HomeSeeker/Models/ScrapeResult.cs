namespace HomeSeeker.Models;

/// <summary>
/// Outcome of a single portal scrape. Distinguishes "portal answered and had no
/// matching listings" from "portal could not be read at all", so the scan can
/// fail (and be retried) instead of recording a bogus zero-result success.
/// </summary>
public sealed record ScrapeResult
{
    /// <summary>
    /// Listings parsed from the portal. Empty on failure.
    /// </summary>
    public IReadOnlyList<ScrapedListing> Listings { get; init; } = [];

    /// <summary>
    /// True when at least one page was fetched and parsed into a recognizable
    /// structure. False when the request failed, was blocked, or the markup
    /// no longer matches the parser.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Short human-readable reason when <see cref="Succeeded"/> is false.
    /// </summary>
    public string? Error { get; init; }

    public static ScrapeResult Success(IReadOnlyList<ScrapedListing> listings) =>
        new() { Listings = listings, Succeeded = true };

    public static ScrapeResult Failure(string error) =>
        new() { Succeeded = false, Error = error };
}
