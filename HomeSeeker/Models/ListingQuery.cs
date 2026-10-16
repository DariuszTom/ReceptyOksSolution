namespace HomeSeeker.Models;

/// <summary>
/// Filters and ordering for browsing stored listings.
/// </summary>
public sealed record ListingQuery
{
    /// <summary>Minimum AI score; null for all listings, including unevaluated ones.</summary>
    public int? MinScore { get; init; }

    /// <summary>
    /// <c>null</c>/<c>"score"</c> (AI score), <c>"price"</c>, <c>"price_desc"</c>, <c>"pricesqm"</c>,
    /// <c>"area_desc"</c>, <c>"distance"</c> or <c>"firstseen"</c>. Unknown values fall back to the AI score.
    /// </summary>
    public string? Sort { get; init; }

    /// <summary>Only this property type; null for any.</summary>
    public PropertyTypes? PropertyType { get; init; }

    /// <summary>Only this portal (case-insensitive); null for any.</summary>
    public string? Portal { get; init; }

    /// <summary>Maximum distance from the city centre; listings with unknown distance are excluded when set.</summary>
    public double? MaxDistanceKm { get; init; }

    /// <summary>Only listings whose price dropped.</summary>
    public bool PriceDropOnly { get; init; }
}
