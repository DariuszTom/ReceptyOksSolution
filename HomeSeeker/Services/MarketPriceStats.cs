using HomeSeeker.Models;

namespace HomeSeeker.Services;

/// <summary>
/// Median price per m² per property type, overall and per district, built from the
/// listings of a single scan. Lets candidates be ranked by how cheap they are
/// <em>for their location</em> instead of in absolute terms, which would always
/// favour the outskirts.
/// </summary>
public sealed class MarketPriceStats
{
    /// <summary>
    /// Minimum listings in a district for its median to be trusted; below this the
    /// city-wide median for the property type is used.
    /// </summary>
    public const int MinDistrictSamples = 5;

    private readonly Dictionary<PropertyTypes, (decimal Median, int Count)> _city = [];
    private readonly Dictionary<(PropertyTypes Type, string District), (decimal Median, int Count)> _districts = [];

    public MarketPriceStats(IEnumerable<HouseListing> listings)
    {
        var priced = listings
            .Where(l => l.Price > 0 && l.AreaSqm > 0)
            .Select(l => (l.PropertyType, District: DistrictOf(l.Location), PerSqm: l.Price / l.AreaSqm))
            .ToList();

        foreach (var group in priced.GroupBy(p => p.PropertyType))
            _city[group.Key] = (Median(group.Select(p => p.PerSqm)), group.Count());

        foreach (var group in priced.Where(p => p.District is not null)
                     .GroupBy(p => (p.PropertyType, District: p.District!)))
        {
            _districts[group.Key] = (Median(group.Select(p => p.PerSqm)), group.Count());
        }
    }

    /// <summary>
    /// Returns the benchmark for the listing's type and district, or null when there
    /// is no priced listing of that type in the scan.
    /// </summary>
    public PriceBenchmark? GetBenchmark(HouseListing listing)
    {
        if (!_city.TryGetValue(listing.PropertyType, out var city))
            return null;

        var district = DistrictOf(listing.Location);
        if (district is not null
            && _districts.TryGetValue((listing.PropertyType, district), out var d)
            && d.Count >= MinDistrictSamples)
        {
            return new PriceBenchmark(d.Median, d.Count, city.Median, city.Count);
        }

        return new PriceBenchmark(null, 0, city.Median, city.Count);
    }

    /// <summary>
    /// Listing price per m² divided by its local reference median (1.0 = typical for
    /// the area, lower = cheaper). Unpriced listings sort last.
    /// </summary>
    public decimal RelativePrice(HouseListing listing)
    {
        if (listing.Price <= 0 || listing.AreaSqm <= 0)
            return decimal.MaxValue;

        var benchmark = GetBenchmark(listing);
        var perSqm = listing.Price / listing.AreaSqm;
        return benchmark is { ReferencePerSqm: > 0 } ? perSqm / benchmark.ReferencePerSqm : perSqm;
    }

    /// <summary>
    /// Both scrapers format location as "City, District"; anything after the first
    /// comma is treated as the district (case-insensitive).
    /// </summary>
    internal static string? DistrictOf(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return null;

        var comma = location.IndexOf(',');
        if (comma < 0)
            return null;

        var district = location[(comma + 1)..].Trim();
        return district.Length == 0 ? null : district.ToLowerInvariant();
    }

    private static decimal Median(IEnumerable<decimal> values)
    {
        var sorted = values.Order().ToArray();
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }
}
