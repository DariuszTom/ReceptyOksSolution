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

    /// <summary>
    /// Kind of property (set by the scraper from the search category).
    /// </summary>
    public PropertyTypes PropertyType { get; init; } = PropertyTypes.House;

    /// <summary>
    /// Number of rooms when the search page provides it; null when unknown.
    /// </summary>
    public int? Rooms { get; init; }

    /// <summary>
    /// Plain-text parameters and description, when the search page already provides them
    /// (OLX). Not persisted; only passed to the AI evaluator.
    /// </summary>
    public string? Details { get; init; }
}
