using System.Xml.Linq;
using ReceptyOks.Views;

namespace ReceptyOks_UnitTests.HomeSeeker;

/// <summary>
/// Guards that the SVG URIs used by the map point at resources embedded in the app assembly
/// (Mapsui fails silently at render time otherwise).
/// </summary>
public sealed class HomeSeekerMapSymbolsTests
{
    [TestCase(HomeSeekerMapSymbols.HomeLocationSource)]
    [TestCase(HomeSeekerMapSymbols.HouseGlyphSource)]
    [TestCase(HomeSeekerMapSymbols.ApartmentGlyphSource)]
    public void SvgSource_IsEmbeddedValidSvg(string source)
    {
        const string scheme = "embedded://";
        Assert.That(source, Does.StartWith(scheme));

        using var stream = typeof(HomeSeekerMapSymbols).Assembly.GetManifestResourceStream(source[scheme.Length..]);

        Assert.That(stream, Is.Not.Null, $"Missing embedded resource for {source}");
        var root = XDocument.Load(stream!).Root!;
        Assert.That(root.Name.LocalName, Is.EqualTo("svg"));
    }
}
