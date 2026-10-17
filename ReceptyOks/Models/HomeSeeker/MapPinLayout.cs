namespace ReceptyOks.Models.HomeSeeker;

/// <summary>A listing placed on the map, with a screen offset that spreads it out of a stack of pins.</summary>
/// <param name="OffsetX">Horizontal offset in device-independent pixels (independent of zoom).</param>
/// <param name="OffsetY">Vertical offset in device-independent pixels; positive is up.</param>
/// <param name="GroupSize">Number of listings sharing (almost) the same location.</param>
public sealed record MapPin(HomeSeekerMapListing Listing, double OffsetX, double OffsetY, int GroupSize)
{
    public bool IsSpread => GroupSize > 1;
}

/// <summary>
/// "Spiderfies" pins: listings within <see cref="SameSpotMeters"/> of each other (several units of
/// one development, a portal placing ads at the street centroid) are laid out on a circle in screen
/// space, so every pin can be seen and tapped at any zoom level.
/// </summary>
public static class MapPinLayout
{
    public const double SameSpotMeters = 10;
    public const double MinRadiusPx = 18;
    public const double RadiusPerPinPx = 4;
    public const double MaxRadiusPx = 60;

    private const double MetersPerDegree = 111_320;

    public static IReadOnlyList<MapPin> Spread(IEnumerable<HomeSeekerMapListing> listings)
    {
        ArgumentNullException.ThrowIfNull(listings);

        var groups = new List<List<HomeSeekerMapListing>>();
        foreach (var listing in listings)
        {
            var group = groups.FirstOrDefault(g => DistanceMeters(g[0], listing) <= SameSpotMeters);
            if (group is null)
                groups.Add([listing]);
            else
                group.Add(listing);
        }

        var pins = new List<MapPin>();
        foreach (var group in groups)
        {
            if (group.Count == 1)
            {
                pins.Add(new MapPin(group[0], 0, 0, 1));
                continue;
            }

            var radius = Math.Clamp(group.Count * RadiusPerPinPx, MinRadiusPx, MaxRadiusPx);
            for (var i = 0; i < group.Count; i++)
            {
                // Start at 12 o'clock and go clockwise, so the best-ranked listing is on top.
                var angle = Math.PI / 2 - 2 * Math.PI * i / group.Count;
                pins.Add(new MapPin(group[i], radius * Math.Cos(angle), radius * Math.Sin(angle), group.Count));
            }
        }

        return pins;
    }

    // Equirectangular approximation: accurate to millimetres over the ~10 m used here.
    private static double DistanceMeters(HomeSeekerMapListing a, HomeSeekerMapListing b)
    {
        var dLat = (a.Latitude - b.Latitude) * MetersPerDegree;
        var dLon = (a.Longitude - b.Longitude) * MetersPerDegree * Math.Cos(a.Latitude * Math.PI / 180);
        return Math.Sqrt(dLat * dLat + dLon * dLon);
    }
}
