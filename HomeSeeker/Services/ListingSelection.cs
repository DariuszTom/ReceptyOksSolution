using HomeSeeker.Models;

namespace HomeSeeker.Services;

/// <summary>
/// Pure filtering and ranking rules used by <see cref="MarketScanService"/>: which scraped
/// listings match the profile and which stored ones are worth a (paid) AI evaluation.
/// </summary>
internal static class ListingSelection
{
    /// <summary>
    /// True when the listing's price, area and room count fall within the profile's ranges.
    /// Unknown values (0 / null, e.g. "zapytaj o cenę") are kept, since they can't be ruled out.
    /// </summary>
    public static bool MatchesCriteria(SearchProfile profile, ScrapedListing listing)
    {
        if (listing.Price > 0)
        {
            if (profile.MinPrice is { } minPrice && listing.Price < minPrice) return false;
            if (profile.MaxPrice is { } maxPrice && listing.Price > maxPrice) return false;
        }

        if (listing.AreaSqm > 0)
        {
            if (profile.MinAreaSqm is { } minArea && listing.AreaSqm < minArea) return false;
            if (profile.MaxAreaSqm is { } maxArea && listing.AreaSqm > maxArea) return false;
        }

        if (profile.MinRooms is { } minRooms && listing.Rooms is { } rooms && rooms < minRooms)
            return false;

        return true;
    }

    /// <summary>
    /// Picks new/price-dropped listings that are cheapest relative to their local market
    /// (price per m² / district median, see <see cref="MarketPriceStats"/>), so central
    /// bargains compete fairly with cheap outskirts. The cap is shared round-robin between
    /// property types so cheaper-per-m² houses can't crowd out apartments; slots a type
    /// can't use go to the other type. Slots left over go to listings still on the market that
    /// were never evaluated (they didn't fit the cap in earlier scans), so the backlog drains
    /// over time instead of staying unscored forever.
    /// Units of one development and copies of one offer on several portals (see
    /// <see cref="ListingGroups"/>) take at most <paramref name="maxPerGroup"/> evaluations in
    /// total, counting ones already scored, since near-identical units would get near-identical
    /// scores and crowd the report. <paramref name="groupingContext"/> (the profile's active
    /// stored listings) lets groups and already-scored counts include listings not seen in this
    /// scan; candidates are still taken only from <paramref name="upsertResults"/>.
    /// </summary>
    public static List<UpsertResult> SelectCandidates(
        IReadOnlyCollection<UpsertResult> upsertResults,
        int max,
        MarketPriceStats? priceStats = null,
        int maxPerGroup = int.MaxValue,
        IEnumerable<HouseListing>? groupingContext = null)
    {
        // A listing repeated in search results is upserted twice (new, then unchanged); keep one
        // occurrence, preferring the new/price-dropped one, so it can't take two paid slots.
        var unique = upsertResults
            .GroupBy(r => r.Listing.Id)
            .Select(g => g.FirstOrDefault(r => r.IsNew || r.PriceDropped) ?? g.First())
            .ToList();

        priceStats ??= new MarketPriceStats(unique.Select(r => r.Listing));

        // This scan's listings first: they carry current price/area and win over stored copies.
        var allListings = unique.Select(r => r.Listing)
            .Concat(groupingContext ?? [])
            .DistinctBy(l => l.Id)
            .ToList();
        var groups = new ListingGroups(allListings);

        // Every listing that already has a score holds a slot in its group, including
        // price-dropped ones about to be re-evaluated.
        var groupCounts = new Dictionary<object, int>();
        foreach (var scored in allListings.Where(l => l.AiScore is not null))
        {
            var group = groups.GroupOf(scored);
            groupCounts[group] = groupCounts.GetValueOrDefault(group) + 1;
        }

        bool TryTakeGroupSlot(UpsertResult result)
        {
            // A re-evaluation reuses the listing's own (already counted) slot.
            if (result.Listing.AiScore is not null) return true;

            var group = groups.GroupOf(result.Listing);
            var count = groupCounts.GetValueOrDefault(group);
            if (count >= maxPerGroup) return false;
            groupCounts[group] = count + 1;
            return true;
        }

        var selected = RoundRobin(unique.Where(r => r.IsNew || r.PriceDropped), max, priceStats, TryTakeGroupSlot);
        if (selected.Count < max)
        {
            var backlog = unique.Where(r => !r.IsNew && !r.PriceDropped && r.Listing.AiScore is null);
            selected.AddRange(RoundRobin(backlog, max - selected.Count, priceStats, TryTakeGroupSlot));
        }

        return selected;
    }

    private static List<UpsertResult> RoundRobin(
        IEnumerable<UpsertResult> results, int max, MarketPriceStats priceStats,
        Func<UpsertResult, bool> tryTakeGroupSlot)
    {
        var queues = results
            .GroupBy(r => r.Listing.PropertyType)
            .OrderBy(g => g.Key)
            .Select(g => new Queue<UpsertResult>(g
                .OrderBy(r => priceStats.RelativePrice(r.Listing))
                .ThenBy(r => PricePerSqm(r.Listing))))
            .ToList();

        var selected = new List<UpsertResult>(max);
        while (selected.Count < max && queues.Any(q => q.Count > 0))
        {
            foreach (var queue in queues)
            {
                if (selected.Count >= max) break;
                // Skip listings whose group is full so the type's turn goes to its next listing.
                while (queue.Count > 0)
                {
                    var next = queue.Dequeue();
                    if (!tryTakeGroupSlot(next)) continue;
                    selected.Add(next);
                    break;
                }
            }
        }

        return selected;
    }

    private static decimal PricePerSqm(HouseListing listing) =>
        listing.AreaSqm > 0 ? listing.Price / listing.AreaSqm : decimal.MaxValue;
}
