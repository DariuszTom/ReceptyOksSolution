using ReceptyOks.Models.HomeSeeker;

namespace ReceptyOks_UnitTests.HomeSeeker;

/// <summary>Spreading ("spiderfying") pins that share a location in <see cref="MapPinLayout"/>.</summary>
public sealed class MapPinLayoutTests
{
    [Test]
    public void Spread_LeavesIsolatedPinsInPlace()
    {
        var pins = MapPinLayout.Spread([At(51.10, 17.03), At(51.11, 17.04)]);

        Assert.That(pins, Has.Count.EqualTo(2));
        Assert.That(pins.All(p => p is { OffsetX: 0, OffsetY: 0, GroupSize: 1, IsSpread: false }), Is.True);
    }

    [Test]
    public void Spread_LaysOutPinsAtTheSameSpotOnACircle_FirstOnTop()
    {
        // Three units ~5 m apart (one development) and one far away.
        var listings = new[] { At(51.10000, 17.03), At(51.10004, 17.03), At(51.10000, 17.03005), At(51.2, 17.1) };

        var pins = MapPinLayout.Spread(listings);

        var group = pins.Where(p => p.IsSpread).ToList();
        Assert.That(group, Has.Count.EqualTo(3));
        Assert.That(group.Select(p => p.Listing), Is.EqualTo(listings.Take(3)));
        Assert.That(group.All(p => p.GroupSize == 3), Is.True);

        var radius = MapPinLayout.MinRadiusPx;
        foreach (var pin in group)
            Assert.That(Math.Sqrt(pin.OffsetX * pin.OffsetX + pin.OffsetY * pin.OffsetY), Is.EqualTo(radius).Within(1e-9));

        // 12 o'clock first (positive Y is up), then clockwise.
        Assert.That(group[0].OffsetX, Is.EqualTo(0).Within(1e-9));
        Assert.That(group[0].OffsetY, Is.EqualTo(radius).Within(1e-9));
        Assert.That(group[1].OffsetX, Is.GreaterThan(0));

        var distinct = group.Select(p => (Math.Round(p.OffsetX, 3), Math.Round(p.OffsetY, 3))).Distinct().Count();
        Assert.That(distinct, Is.EqualTo(3), "every pin gets its own position");

        Assert.That(pins.Single(p => !p.IsSpread).Listing, Is.SameAs(listings[3]));
    }

    [Test]
    public void Spread_RadiusGrowsWithGroupSize_UpToMaximum()
    {
        var big = MapPinLayout.Spread(Enumerable.Range(0, 40).Select(_ => At(51.1, 17.03)).ToList());
        var medium = MapPinLayout.Spread(Enumerable.Range(0, 10).Select(_ => At(51.1, 17.03)).ToList());

        static double Radius(MapPin p) => Math.Sqrt(p.OffsetX * p.OffsetX + p.OffsetY * p.OffsetY);
        Assert.That(Radius(big[0]), Is.EqualTo(MapPinLayout.MaxRadiusPx).Within(1e-9));
        Assert.That(Radius(medium[0]), Is.EqualTo(10 * MapPinLayout.RadiusPerPinPx).Within(1e-9));
    }

    [Test]
    public void Spread_PinsFartherThanThreshold_AreNotGrouped()
    {
        // ~22 m apart in latitude.
        var pins = MapPinLayout.Spread([At(51.1, 17.03), At(51.1002, 17.03)]);

        Assert.That(pins.Any(p => p.IsSpread), Is.False);
    }

    private static HomeSeekerMapListing At(double lat, double lon) => new()
    {
        Id = Guid.NewGuid(),
        Title = "x",
        Latitude = lat,
        Longitude = lon
    };
}
