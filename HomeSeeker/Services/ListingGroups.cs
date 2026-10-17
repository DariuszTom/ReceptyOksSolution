using HomeSeeker.Models;

namespace HomeSeeker.Services;

/// <summary>
/// Groups listings that are effectively the same offer: units of one developer's investment
/// (same <see cref="HouseListing.DevelopmentKey"/>) and copies of one ad posted on several
/// portals (same property type, price and area, or near-identical price/area at the same
/// exact map location). Groups are transitive, so an Otodom unit
/// with a development id and its OLX copy without one end up together.
/// </summary>
public sealed class ListingGroups
{
    private readonly Dictionary<Guid, int> _parent = [];

    public ListingGroups(IEnumerable<HouseListing> listings)
    {
        ArgumentNullException.ThrowIfNull(listings);

        var index = new Dictionary<Guid, int>();
        var roots = new List<int>();
        var byKey = new Dictionary<string, int>(StringComparer.Ordinal);
        var located = new List<(HouseListing Listing, GeoPoint Point)>();

        foreach (var listing in listings)
        {
            if (index.ContainsKey(listing.Id)) continue;
            var node = roots.Count;
            index[listing.Id] = node;
            roots.Add(node);

            // Approximate pins (OLX radius) are too coarse to identify a building.
            if (listing is { Latitude: { } lat, Longitude: { } lon, CoordinatesApproximate: false } &&
                new GeoPoint(lat, lon) is { IsValid: true } point)
            {
                located.Add((listing, point));
            }

            foreach (var key in Keys(listing))
            {
                if (byKey.TryGetValue(key, out var other))
                    Union(roots, node, other);
                else
                    byKey[key] = node;
            }
        }

        UnionNearbyCopies(roots, index, located);

        foreach (var (id, node) in index)
            _parent[id] = Find(roots, node);
    }

    /// <summary>Exact pins closer than this are treated as the same building.</summary>
    internal const double SameLocationKm = 0.03;
    internal const decimal AreaTolerance = 0.02m;
    internal const decimal PriceTolerance = 0.05m;

    // ~55 m in latitude; neighbouring cells cover SameLocationKm in every direction at Polish latitudes.
    private const double CellDegrees = 0.0005;

    /// <summary>
    /// Joins cross-portal copies whose price or area was rounded differently (so the exact
    /// "dup:" key misses them): same type, exact pins within <see cref="SameLocationKm"/>,
    /// area within 2% and price within 5%.
    /// </summary>
    private static void UnionNearbyCopies(
        List<int> roots, Dictionary<Guid, int> index, List<(HouseListing Listing, GeoPoint Point)> located)
    {
        if (located.Count < 2) return;

        var cells = new Dictionary<(long, long), List<(HouseListing Listing, GeoPoint Point)>>();
        foreach (var item in located)
        {
            var (cy, cx) = Cell(item.Point);
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                if (!cells.TryGetValue((cy + dy, cx + dx), out var neighbours)) continue;
                foreach (var other in neighbours)
                {
                    if (IsSameOffer(item.Listing, item.Point, other.Listing, other.Point))
                        Union(roots, index[item.Listing.Id], index[other.Listing.Id]);
                }
            }

            if (!cells.TryGetValue((cy, cx), out var own))
                cells[(cy, cx)] = own = [];
            own.Add(item);
        }
    }

    private static (long, long) Cell(GeoPoint point) =>
        ((long)Math.Floor(point.Latitude / CellDegrees),
         (long)Math.Floor(point.Longitude / (CellDegrees / Math.Max(0.1, Math.Cos(point.Latitude * Math.PI / 180)))));

    private static bool IsSameOffer(HouseListing a, GeoPoint pa, HouseListing b, GeoPoint pb) =>
        a.PropertyType == b.PropertyType &&
        WithinRatio(a.AreaSqm, b.AreaSqm, AreaTolerance) &&
        WithinRatio(a.Price, b.Price, PriceTolerance) &&
        pa.DistanceKmTo(pb) <= SameLocationKm;

    private static bool WithinRatio(decimal a, decimal b, decimal tolerance) =>
        a > 0 && b > 0 && Math.Abs(a - b) <= Math.Max(a, b) * tolerance;

    /// <summary>Group identifier of the listing; listings not passed to the constructor form their own group.</summary>
    public object GroupOf(HouseListing listing) =>
        _parent.TryGetValue(listing.Id, out var root) ? root : listing.Id;

    /// <summary>
    /// Keeps at most <paramref name="maxPerGroup"/> listings from each group, preserving the
    /// input order (so pass listings best-first).
    /// </summary>
    /// <param name="groups">
    /// Optional precomputed groups (e.g. built from all active listings, so unscored "bridge"
    /// listings still join their scored relatives); built from the input when null.
    /// </param>
    public static List<HouseListing> CapPerGroup(
        IEnumerable<HouseListing> orderedBestFirst, int maxPerGroup, ListingGroups? groups = null)
    {
        var list = orderedBestFirst.ToList();
        groups ??= new ListingGroups(list);
        var counts = new Dictionary<object, int>();
        var result = new List<HouseListing>(list.Count);

        foreach (var listing in list)
        {
            var group = groups.GroupOf(listing);
            var count = counts.GetValueOrDefault(group);
            if (count >= maxPerGroup) continue;
            counts[group] = count + 1;
            result.Add(listing);
        }

        return result;
    }

    private static IEnumerable<string> Keys(HouseListing listing)
    {
        if (!string.IsNullOrWhiteSpace(listing.DevelopmentKey))
            yield return "dev:" + listing.DevelopmentKey;

        // Unknown price/area ("zapytaj o cenę") would lump unrelated offers together.
        // Fixed-precision formats so 80m and 80.0m (different decimal scale) produce the same key.
        if (listing.Price > 0 && listing.AreaSqm > 0)
            yield return FormattableString.Invariant(
                $"dup:{listing.PropertyType}|{decimal.Round(listing.Price):F0}|{decimal.Round(listing.AreaSqm, 1):F1}");
    }

    private static int Find(List<int> roots, int node)
    {
        while (roots[node] != node)
        {
            roots[node] = roots[roots[node]];
            node = roots[node];
        }
        return node;
    }

    private static void Union(List<int> roots, int a, int b)
    {
        var rootA = Find(roots, a);
        var rootB = Find(roots, b);
        if (rootA != rootB) roots[rootA] = rootB;
    }
}
