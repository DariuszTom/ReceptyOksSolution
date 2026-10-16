namespace HomeSeeker.Models;

/// <summary>
/// Data read from a listing's own page.
/// </summary>
/// <param name="Text">Plain-text parameters and description for AI evaluation, or null.</param>
/// <param name="Coordinates">Listing coordinates when the page exposes them.</param>
public sealed record ListingDetails(string? Text, GeoPoint? Coordinates = null);
