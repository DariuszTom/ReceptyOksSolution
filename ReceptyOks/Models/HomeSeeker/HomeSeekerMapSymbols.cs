using Mapsui.Styles;
using ReceptyOks.Models.HomeSeeker;
using Color = Mapsui.Styles.Color;
using Brush = Mapsui.Styles.Brush;
using Image = Mapsui.Styles.Image;

namespace ReceptyOks.Views;

/// <summary>
/// Mapsui styles for the HomeSeeker map. SVG glyphs live in <c>Resources/Map</c> as embedded
/// resources (see csproj) and are loaded by Mapsui through <c>embedded://</c> URIs.
/// </summary>
public static class HomeSeekerMapSymbols
{
    private const string ResourcePrefix = "embedded://ReceptyOks.Resources.Map.";

    public const string HomeLocationSource = ResourcePrefix + "HomeLocation.svg";
    public const string HouseGlyphSource = ResourcePrefix + "PinHouse.svg";
    public const string ApartmentGlyphSource = ResourcePrefix + "PinApartment.svg";

    private const double PinScale = 0.7;
    private const double ApproximatePinScale = 0.95;

    public static readonly IStyle Home = new ImageStyle { Image = new Image { Source = HomeLocationSource } };

    public static readonly IStyle GroupCenter = new SymbolStyle
    {
        SymbolType = SymbolType.Ellipse,
        SymbolScale = 0.2,
        Fill = new Brush(new Color(60, 60, 60)),
        Outline = new Pen(Color.White, 1)
    };

    /// <summary>
    /// Score-coloured disc with a house/apartment glyph; approximate locations (e.g. OLX radius)
    /// get a larger translucent disc and a glyph in the score colour.
    /// </summary>
    public static IEnumerable<IStyle> ForPin(MapPin pin)
    {
        var approximate = pin.Listing.CoordinatesApproximate;
        var color = ScoreColor(pin.Listing.AiScore);
        var scale = approximate ? ApproximatePinScale : PinScale;

        // Mapsui multiplies Offset by SymbolScale; divide so both layers move by the layout's pixels.
        yield return new SymbolStyle
        {
            SymbolType = SymbolType.Ellipse,
            SymbolScale = scale,
            Fill = new Brush(approximate ? new Color(color.R, color.G, color.B, 90) : color),
            Outline = new Pen(Color.White, approximate ? 1 : 2),
            Offset = new Offset(pin.OffsetX / scale, pin.OffsetY / scale)
        };

        yield return new ImageStyle
        {
            Image = new Image
            {
                Source = pin.Listing.PropertyType == HomeSeekerPropertyTypes.Apartment
                    ? ApartmentGlyphSource
                    : HouseGlyphSource,
                SvgFillColor = approximate ? Darken(color) : null
            },
            Offset = new Offset(pin.OffsetX, pin.OffsetY)
        };
    }

    private static Color ScoreColor(int? score) => score switch
    {
        >= 75 => new Color(46, 160, 67),
        >= 50 => new Color(230, 170, 20),
        _ => new Color(140, 140, 140)
    };

    private static Color Darken(Color color) =>
        new((int)(color.R * 0.6), (int)(color.G * 0.6), (int)(color.B * 0.6));
}
