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
}
