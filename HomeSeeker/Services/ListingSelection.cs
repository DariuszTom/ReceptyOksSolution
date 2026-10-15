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
    /// </summary>
    public static List<UpsertResult> SelectCandidates(
        IReadOnlyCollection<UpsertResult> upsertResults, int max, MarketPriceStats? priceStats = null)
    {
        // A listing repeated in search results is upserted twice (new, then unchanged); keep one
        // occurrence, preferring the new/price-dropped one, so it can't take two paid slots.
        var unique = upsertResults
            .GroupBy(r => r.Listing.Id)
            .Select(g => g.FirstOrDefault(r => r.IsNew || r.PriceDropped) ?? g.First())
            .ToList();

        priceStats ??= new MarketPriceStats(unique.Select(r => r.Listing));

        var selected = RoundRobin(unique.Where(r => r.IsNew || r.PriceDropped), max, priceStats);
        if (selected.Count < max)
        {
            var backlog = unique.Where(r => !r.IsNew && !r.PriceDropped && r.Listing.AiScore is null);
            selected.AddRange(RoundRobin(backlog, max - selected.Count, priceStats));
        }

        return selected;
    }

    private static List<UpsertResult> RoundRobin(
        IEnumerable<UpsertResult> results, int max, MarketPriceStats priceStats)
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
                if (queue.Count > 0) selected.Add(queue.Dequeue());
            }
        }

        return selected;
    }

    private static decimal PricePerSqm(HouseListing listing) =>
        listing.AreaSqm > 0 ? listing.Price / listing.AreaSqm : decimal.MaxValue;
}
