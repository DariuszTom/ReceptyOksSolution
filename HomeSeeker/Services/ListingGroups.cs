using HomeSeeker.Models;

namespace HomeSeeker.Services;

/// <summary>
/// Groups listings that are effectively the same offer: units of one developer's investment
/// (same <see cref="HouseListing.DevelopmentKey"/>) and copies of one ad posted on several
/// portals (same property type, price and area). Groups are transitive, so an Otodom unit
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

        foreach (var listing in listings)
        {
            if (index.ContainsKey(listing.Id)) continue;
            var node = roots.Count;
            index[listing.Id] = node;
            roots.Add(node);

            foreach (var key in Keys(listing))
            {
                if (byKey.TryGetValue(key, out var other))
                    Union(roots, node, other);
                else
                    byKey[key] = node;
            }
        }

        foreach (var (id, node) in index)
            _parent[id] = Find(roots, node);
    }

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
