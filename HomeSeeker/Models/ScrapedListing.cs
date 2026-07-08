namespace HomeSeeker.Models;

/// <summary>
/// Represents a listing scraped from a portal (DTO from scraper).
/// </summary>
public sealed record ScrapedListing
{
    /// <summary>
    /// Portal name (e.g., "Otodom", "OLX").
    /// </summary>
    public required string Portal { get; init; }

    /// <summary>
    /// External ID from the portal.
    /// </summary>
    public required string ExternalId { get; init; }

    /// <summary>
    /// Full URL to the listing.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Listing title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Price in PLN.
    /// </summary>
    public required decimal Price { get; init; }

    /// <summary>
    /// Area in square meters.
    /// </summary>
    public required decimal AreaSqm { get; init; }

    /// <summary>
    /// Location description.
    /// </summary>
    public string? Location { get; init; }
}
