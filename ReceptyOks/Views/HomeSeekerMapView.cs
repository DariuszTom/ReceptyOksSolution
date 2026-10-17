using Mapsui;
using Mapsui.Layers;
using Mapsui.Manipulations;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using ReceptyOks.Models.HomeSeeker;
using ReceptyOks.ViewModels;
using Color = Mapsui.Styles.Color;
using Brush = Mapsui.Styles.Brush;

namespace ReceptyOks.Views;

/// <summary>
/// Builds the Mapsui map for the HomeSeeker "Mapa" tab: OpenStreetMap tiles, listing pins coloured
/// by score (spread out when they share a spot) and "our location". Created lazily, only when the
/// tab is first shown.
/// </summary>
internal sealed class HomeSeekerMapView
{
    private const string ListingKey = "listing";
    private const string UserAgent = "ReceptyOks-HomeSeeker/1.0 (+https://github.com/DariuszTom/ReceptyOksSolution)";

    // Wrocław; replaced by the pins' extent as soon as data arrives.
    private const double DefaultLatitude = 51.11;
    private const double DefaultLongitude = 17.03;

    private readonly HomeSeekerViewModel _viewModel;
    private readonly Func<string, string, Task<bool>> _confirm;
    private readonly MemoryLayer _pinLayer = new("Oferty") { Style = null };
    private readonly MemoryLayer _homeLayer = new("Nasza lokalizacja") { Style = null };
    private bool _zoomedToData;

    public HomeSeekerMapView(HomeSeekerViewModel viewModel, Func<string, string, Task<bool>> confirm)
    {
        _viewModel = viewModel;
        _confirm = confirm;

        var map = new Mapsui.Map();
        map.Layers.Add(OpenStreetMap.CreateTileLayer(UserAgent));
        map.Layers.Add(_homeLayer);
        map.Layers.Add(_pinLayer);
        map.Tapped += OnMapTapped;

        Control = new MapControl { Map = map };

        var (x, y) = SphericalMercator.FromLonLat(DefaultLongitude, DefaultLatitude);
        map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), map.Navigator.Resolutions.Count > 12 ? map.Navigator.Resolutions[12] : 20);

        _viewModel.MapDataChanged += OnMapDataChanged;
        Redraw();
    }

    public MapControl Control { get; }

    public void Detach() => _viewModel.MapDataChanged -= OnMapDataChanged;

    private void OnMapDataChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(Redraw);

    private void Redraw()
    {
        var pins = _viewModel.MapPins;
        var features = new List<IFeature>();

        // Centre dots first, so the spread pins are drawn above them.
        foreach (var group in pins.Where(p => p.IsSpread).GroupBy(p => p.Listing.Latitude + "," + p.Listing.Longitude))
        {
            var first = group.First().Listing;
            features.Add(CreatePoint(first.Latitude, first.Longitude, CenterDotStyle));
        }

        foreach (var pin in pins)
        {
            var feature = CreatePoint(pin.Listing.Latitude, pin.Listing.Longitude, PinStyle(pin));
            feature[ListingKey] = pin.Listing;
            features.Add(feature);
        }

        _pinLayer.Features = features;

        _homeLayer.Features = _viewModel.HomeLatitude is { } lat && _viewModel.HomeLongitude is { } lon
            ? [CreatePoint(lat, lon, HomeStyle)]
            : [];

        _pinLayer.DataHasChanged();
        _homeLayer.DataHasChanged();

        if (!_zoomedToData && pins.Count > 0)
        {
            _zoomedToData = true;
            ZoomToPins(pins);
        }

        Control.RefreshGraphics();
    }

    private void ZoomToPins(IReadOnlyList<MapPin> pins)
    {
        var points = pins.Select(p => SphericalMercator.FromLonLat(p.Listing.Longitude, p.Listing.Latitude)).ToList();
        if (_viewModel.HomeLatitude is { } lat && _viewModel.HomeLongitude is { } lon)
            points.Add(SphericalMercator.FromLonLat(lon, lat));

        var box = new MRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
        // Keep a minimum extent (~1 km) so a single pin is not zoomed in to street level.
        box = box.Grow(Math.Max(500, Math.Max(box.Width, box.Height) * 0.1));
        Control.Map.Navigator.ZoomToBox(box, MBoxFit.Fit);
    }

    private async void OnMapTapped(object? sender, MapEventArgs e)
    {
        try
        {
            if (e.GestureType == GestureType.LongPress)
            {
                e.Handled = true;
                var (lon, lat) = SphericalMercator.ToLonLat(e.WorldPosition.X, e.WorldPosition.Y);
                var text = $"Ustawić naszą lokalizację w punkcie {HomeSeekerViewModel.FormatCoordinates(lat, lon)}?\n"
                    + "Odległości ofert będą liczone od tego miejsca (także w ocenie AI).";
                if (await _confirm("Nasza lokalizacja", text))
                    await _viewModel.SetHomeAsync(lat, lon, "Wybrana na mapie");
                return;
            }

            if (e.GestureType != GestureType.SingleTap)
                return;

            var info = e.GetMapInfo([_pinLayer]);
            _viewModel.SelectedMapListing = info.Feature?[ListingKey] as HomeSeekerMapListing;
            e.Handled = info.Feature is not null;
        }
        catch (Exception)
        {
            // Never crash the app from a map gesture.
        }
    }

    private static PointFeature CreatePoint(double latitude, double longitude, IStyle style)
    {
        var (x, y) = SphericalMercator.FromLonLat(longitude, latitude);
        var feature = new PointFeature(x, y);
        feature.Styles.Add(style);
        return feature;
    }

    private static Color ScoreColor(int? score) => score switch
    {
        >= 75 => new Color(46, 160, 67),
        >= 50 => new Color(230, 170, 20),
        _ => new Color(140, 140, 140)
    };

    private static SymbolStyle PinStyle(MapPin pin)
    {
        var approximate = pin.Listing.CoordinatesApproximate;
        var color = ScoreColor(pin.Listing.AiScore);
        return new SymbolStyle
        {
            SymbolType = SymbolType.Ellipse,
            // Approximate locations (e.g. OLX radius): a larger translucent disc instead of a solid pin.
            SymbolScale = approximate ? 0.9 : 0.55,
            Fill = new Brush(approximate ? new Color(color.R, color.G, color.B, 90) : color),
            Outline = new Pen(Color.White, approximate ? 1 : 2),
            Offset = new Offset(pin.OffsetX, pin.OffsetY)
        };
    }

    private static readonly SymbolStyle CenterDotStyle = new()
    {
        SymbolType = SymbolType.Ellipse,
        SymbolScale = 0.2,
        Fill = new Brush(new Color(60, 60, 60)),
        Outline = new Pen(Color.White, 1)
    };

    private static readonly SymbolStyle HomeStyle = new()
    {
        SymbolType = SymbolType.Rectangle,
        SymbolScale = 0.7,
        Fill = new Brush(new Color(33, 102, 214)),
        Outline = new Pen(Color.White, 3)
    };
}
