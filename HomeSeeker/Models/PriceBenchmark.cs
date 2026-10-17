namespace HomeSeeker.Models;

/// <summary>
/// Local market reference for a listing, computed from all listings of the same
/// property type found in the current scan.
/// </summary>
/// <param name="DistrictMedianPerSqm">Median price per m² in the listing's district, or null when the district has too few listings.</param>
/// <param name="DistrictSampleSize">Number of listings behind <paramref name="DistrictMedianPerSqm"/>.</param>
/// <param name="CityMedianPerSqm">Median price per m² across the whole scan for this property type.</param>
/// <param name="CitySampleSize">Number of listings behind <paramref name="CityMedianPerSqm"/>.</param>
public sealed record PriceBenchmark(
    decimal? DistrictMedianPerSqm,
    int DistrictSampleSize,
    decimal CityMedianPerSqm,
    int CitySampleSize)
{
    /// <summary>
    /// The most local median available: district when reliable, otherwise city.
    /// </summary>
    public decimal ReferencePerSqm => DistrictMedianPerSqm ?? CityMedianPerSqm;

    /// <summary>
    /// Straight-line distance from the city centre, when the listing's coordinates are known.
    /// </summary>
    public double? DistanceFromCenterKm { get; init; }

    /// <summary>
    /// True when <see cref="DistanceFromCenterKm"/> is based on an approximate portal location.
    /// </summary>
    public bool DistanceIsApproximate { get; init; }

    /// <summary>
    /// True when <see cref="DistanceFromCenterKm"/> is measured from the buyer's chosen location
    /// instead of the city centre.
    /// </summary>
    public bool DistanceFromHome { get; init; }

    /// <summary>
    /// Transaction-price (RCN) valuation near the listing, when available (apartments only).
    /// </summary>
    public RcnValuation? Rcn { get; init; }
}
