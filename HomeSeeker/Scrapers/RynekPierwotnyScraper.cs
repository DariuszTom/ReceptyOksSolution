using System.Globalization;
using System.Text;
using System.Text.Json;
using HomeSeeker.Configuration;
using HomeSeeker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static HomeSeeker.Scrapers.ScraperHelper;

namespace HomeSeeker.Scrapers;

/// <summary>
/// Scraper for the primary market portal rynekpierwotny.pl. Each search result is a developer's
/// investment (not a single unit): its <c>window.__INITIAL_STATE__</c> JSON gives price, area,
/// price/m² and room ranges, coordinates, developer and completion date. An investment becomes one
/// listing whose price/area estimate the cheapest unit fitting the profile; the investment page
/// lists only its first 20 units and can't be filtered, so individual units are not scraped.
/// </summary>
public sealed class RynekPierwotnyScraper : ListingScraperBase
{
    private const string BaseUrl = "https://rynekpierwotny.pl";
    private const string Domain = "rynekpierwotny.pl";
    private const string StateMarker = "window.__INITIAL_STATE__";

    public RynekPierwotnyScraper(
        IHttpClientFactory httpClientFactory,
        IOptions<HomeSeekerOptions> options,
        ILogger<RynekPierwotnyScraper> logger)
        : base(httpClientFactory, options, logger)
    {
    }

    public override string PortalName => PortalNames.RynekPierwotny;

    protected override string PortalDomain => Domain;

    /// <summary>
    /// <c>/s/nowe-mieszkania-{city}/</c> or <c>/s/nowe-domy-{city}/</c>, with <c>?page=N</c>.
    /// The portal's price/area/rooms filters (<c>price_0</c>, <c>area_0</c>, <c>rooms_0</c>…) are not
    /// used: such uncached queries took ~30 s and failed with 500 (2026-10-20), while plain city pages
    /// answer instantly. The scan filters by the profile anyway (<c>ListingSelection.MatchesCriteria</c>).
    /// </summary>
    protected override string BuildSearchUrl(SearchProfile profile, PropertyTypes type, int page)
    {
        var category = type == PropertyTypes.Apartment ? "nowe-mieszkania" : "nowe-domy";
        var url = $"{BaseUrl}/s/{category}-{Uri.EscapeDataString(NormalizeCity(profile.City.Trim()))}/";
        return page > 1 ? $"{url}?page={page.ToString(CultureInfo.InvariantCulture)}" : url;
    }

    protected override IReadOnlyList<ScrapedListing> ParseSearchPage(string html) =>
        ParseSearchResults(html, profile: null, expectedPage: 1).Listings;

    protected override IReadOnlyList<ScrapedListing> ParseSearchPage(string html, SearchProfile profile, PropertyTypes type, int page)
    {
        var result = ParseSearchResults(html, profile, page);
        if (result.Problem is not null)
            Logger.LogWarning("{Portal} {Type} page {Page} for {City}: {Problem}", PortalName, type, page, profile.City, result.Problem);
        return result.Listings;
    }

    protected override ListingDetails? ParseDetailsPage(string html)
    {
        using var state = ReadInitialState(html);
        if (state is null ||
            !TryGetPath(state.RootElement, out var offer, "offerDetail", "offer") ||
            offer.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return CreateDetails(BuildDetails(offer), ReadCoordinates(offer));
    }

    internal sealed record SearchPageResult(List<ScrapedListing> Listings, string? Problem = null);

    /// <summary>
    /// Investments from a search page. Empty (with a problem description) when the portal redirected
    /// elsewhere: an unknown city lands on the country-wide list and a page past the end on page 1,
    /// both of which would otherwise yield unrelated or duplicate results.
    /// </summary>
    internal static SearchPageResult ParseSearchResults(string html, SearchProfile? profile, int expectedPage)
    {
        using var state = ReadInitialState(html);
        if (state is null || !TryGetPath(state.RootElement, out var list, "offerList", "list"))
            return new([], "brak danych __INITIAL_STATE__");

        if (list.TryGetProperty("latestQuery", out var query) && query.ValueKind == JsonValueKind.Object)
        {
            if (!query.TryGetProperty("region", out _))
                return new([], "nieznane miasto (portal przekierował na listę ogólnopolską)");

            var page = GetInt(query, "page") ?? 1;
            if (page != expectedPage)
                return new([]);
        }

        if (!list.TryGetProperty("offers", out var offers) || offers.ValueKind != JsonValueKind.Array)
            return new([]);

        var listings = new List<ScrapedListing>();
        foreach (var offer in offers.EnumerateArray())
        {
            if (offer.ValueKind == JsonValueKind.Object && FromOffer(offer, profile) is { } listing)
                listings.Add(listing);
        }

        return new(listings);
    }

    private static ScrapedListing? FromOffer(JsonElement offer, SearchProfile? profile)
    {
        var id = GetInt(offer, "id");
        var slug = GetString(offer, "slug");
        var vendorSlug = offer.TryGetProperty("vendor", out var vendor) && vendor.ValueKind == JsonValueKind.Object
            ? GetString(vendor, "slug")
            : null;
        if (id is not > 0 || string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(vendorSlug))
            return null;

        if (!offer.TryGetProperty("stats", out var stats) || stats.ValueKind != JsonValueKind.Object)
            return null;

        // Hidden prices ("zapytaj o cenę") can't be compared or ranked.
        var pricesHidden = offer.TryGetProperty("configuration", out var config) &&
                           config.ValueKind == JsonValueKind.Object &&
                           config.TryGetProperty("show_price_range", out var show) &&
                           show.ValueKind == JsonValueKind.False;
        var ranges = Ranges.From(stats);
        if (pricesHidden || ranges.PriceMin is not > 0 || ranges.AreaMin is not > 0)
            return null;

        var (price, area, rooms) = EstimateUnit(ranges, profile);
        var url = $"{BaseUrl}/oferty/{Uri.EscapeDataString(vendorSlug)}/{Uri.EscapeDataString(slug)}-{id.Value.ToString(CultureInfo.InvariantCulture)}/";
        var name = GetString(offer, "name") ?? "Inwestycja";
        var vendorName = GetNestedName(offer, "vendor");

        return new ScrapedListing
        {
            Portal = PortalNames.RynekPierwotny,
            ExternalId = id.Value.ToString(CultureInfo.InvariantCulture),
            Url = url,
            Title = string.IsNullOrWhiteSpace(vendorName) ? name : $"{name} – {vendorName}",
            Price = price,
            AreaSqm = area,
            Location = ReadLocation(offer),
            Rooms = rooms,
            DevelopmentKey = $"rynekpierwotny:{id.Value.ToString(CultureInfo.InvariantCulture)}",
            Details = BuildDetails(offer),
            Coordinates = ReadCoordinates(offer)
        };
    }

    /// <summary>
    /// Estimates the cheapest unit of the investment that fits the profile: the smallest area
    /// within both ranges, priced at the lowest price/m² for that area (at least the minimum price),
    /// then clamped into the overlap of the investment's and the profile's price ranges (a pricier unit
    /// of a matching size may exist within the range). When the ranges don't overlap the raw estimate is kept,
    /// so the profile filter rejects it. Ranges cover the whole investment: this is an estimate, not a real unit.
    /// </summary>
    internal static (decimal Price, decimal Area, int? Rooms) EstimateUnit(Ranges ranges, SearchProfile? profile)
    {
        var areaMin = ranges.AreaMin!.Value;
        var area = FitToOverlap(areaMin, areaMin, ranges.AreaMax, profile?.MinAreaSqm, profile?.MaxAreaSqm);

        var priceMin = ranges.PriceMin!.Value;
        var estimate = Math.Max(priceMin, ranges.PriceM2Min is > 0 ? Math.Round(area * ranges.PriceM2Min.Value) : 0);
        var price = FitToOverlap(estimate, priceMin, ranges.PriceMax, profile?.MinPrice, profile?.MaxPrice);

        int? rooms = null;
        if (ranges.RoomsMin is > 0)
        {
            rooms = ranges.RoomsMin.Value;
            if (profile?.MinRooms is { } minRooms && minRooms > rooms && (ranges.RoomsMax is null || minRooms <= ranges.RoomsMax))
                rooms = minRooms;
        }

        return (price, area, rooms);
    }

    private static decimal FitToOverlap(decimal value, decimal low, decimal? high, decimal? profileMin, decimal? profileMax)
    {
        var lower = Math.Max(low, profileMin ?? 0);
        var upper = Math.Min(high is > 0 ? high.Value : decimal.MaxValue, profileMax ?? decimal.MaxValue);
        return lower <= upper ? Math.Clamp(value, lower, upper) : value;
    }

    internal sealed record Ranges(
        decimal? PriceMin, decimal? PriceMax, decimal? PriceM2Min, decimal? PriceM2Max,
        decimal? AreaMin, decimal? AreaMax, int? RoomsMin, int? RoomsMax)
    {
        public static Ranges From(JsonElement stats) => new(
            GetDecimal(stats, "ranges_price_min"), GetDecimal(stats, "ranges_price_max"),
            GetDecimal(stats, "ranges_price_m2_min"), GetDecimal(stats, "ranges_price_m2_max"),
            GetDecimal(stats, "ranges_area_min"), GetDecimal(stats, "ranges_area_max"),
            (int?)GetDecimal(stats, "ranges_rooms_min"), (int?)GetDecimal(stats, "ranges_rooms_max"));
    }

    /// <summary>"Kraków, Dębniki" (region short name, matching the "City, District" convention).</summary>
    private static string? ReadLocation(JsonElement offer)
    {
        if (offer.TryGetProperty("region", out var region) && region.ValueKind == JsonValueKind.Object &&
            GetString(region, "short_name") is { Length: > 0 } shortName)
        {
            return shortName;
        }

        return GetString(offer, "address");
    }

    /// <summary>GeoJSON <c>geo_point.coordinates</c> = [lon, lat]; precise (the investment's address).</summary>
    internal static GeoPoint? ReadCoordinates(JsonElement offer)
    {
        if (!offer.TryGetProperty("geo_point", out var geo) || geo.ValueKind != JsonValueKind.Object ||
            !geo.TryGetProperty("coordinates", out var coords) || coords.ValueKind != JsonValueKind.Array ||
            coords.GetArrayLength() < 2)
        {
            return null;
        }

        var lon = coords[0];
        var lat = coords[1];
        if (lon.ValueKind != JsonValueKind.Number || lat.ValueKind != JsonValueKind.Number)
            return null;

        var point = new GeoPoint(lat.GetDouble(), lon.GetDouble());
        return point.IsValid ? point : null;
    }

    /// <summary>Investment parameters (ranges, developer, completion) and description for the AI.</summary>
    internal static string? BuildDetails(JsonElement offer)
    {
        var parameters = new List<(string, string)>
        {
            ("Rynek", "pierwotny – inwestycja deweloperska; cena i metraż ogłoszenia to szacunek najtańszego lokalu pasującego do profilu, poniżej zakresy całej inwestycji")
        };

        if (GetNestedName(offer, "vendor") is { Length: > 0 } vendor)
            parameters.Add(("Deweloper", vendor));
        if (GetString(offer, "address") is { Length: > 0 } address)
            parameters.Add(("Adres", address));

        if (offer.TryGetProperty("stats", out var stats) && stats.ValueKind == JsonValueKind.Object)
        {
            var r = Ranges.From(stats);
            AddRange(parameters, "Ceny lokali", r.PriceMin, r.PriceMax, "zł");
            AddRange(parameters, "Cena za m²", r.PriceM2Min, r.PriceM2Max, "zł");
            AddRange(parameters, "Metraże", r.AreaMin, r.AreaMax, "m²");
            AddRange(parameters, "Pokoje", r.RoomsMin, r.RoomsMax, null);
            if (GetDecimal(stats, "properties_count_for_sale") is > 0 and var forSale)
                parameters.Add(("Lokali w sprzedaży", Format(forSale)));
            if (stats.TryGetProperty("distance_from_region", out var distance) && distance.ValueKind == JsonValueKind.Object &&
                GetDecimal(distance, "distance") is > 0 and var km)
            {
                parameters.Add(("Odległość od centrum (wg portalu)", $"{km.ToString("0.#", CultureInfo.InvariantCulture)} km"));
            }
        }

        if (offer.TryGetProperty("construction_date_range", out var construction) && construction.ValueKind == JsonValueKind.Object)
        {
            var from = GetString(construction, "lower");
            var to = GetString(construction, "upper");
            if (!string.IsNullOrWhiteSpace(to))
                parameters.Add(("Termin oddania", string.IsNullOrWhiteSpace(from) || from == to ? to : $"{from} – {to}"));
        }

        return ListingDetailsText.Build(parameters, GetString(offer, "description"));
    }

    private static void AddRange(List<(string, string)> parameters, string name, decimal? min, decimal? max, string? unit)
    {
        if (min is not > 0)
            return;

        var suffix = unit is null ? string.Empty : " " + unit;
        var value = max is > 0 && max != min ? $"{Format(min.Value)} – {Format(max.Value)}{suffix}" : $"{Format(min.Value)}{suffix}";
        parameters.Add((name, value));
    }

    private static void AddRange(List<(string, string)> parameters, string name, int? min, int? max, string? unit) =>
        AddRange(parameters, name, (decimal?)min, max, unit);

    private static string Format(decimal value) =>
        value.ToString(value == Math.Floor(value) ? "#,0" : "#,0.##", CultureInfo.GetCultureInfo("pl-PL"));

    /// <summary>Parses the JSON object assigned to <c>window.__INITIAL_STATE__</c>; null when missing or invalid.</summary>
    internal static JsonDocument? ReadInitialState(string html)
    {
        var marker = html.IndexOf(StateMarker, StringComparison.Ordinal);
        if (marker < 0)
            return null;

        var start = html.IndexOf('{', marker + StateMarker.Length);
        if (start < 0)
            return null;

        try
        {
            // The object is followed by ";" and more script; read exactly one JSON value.
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(html, start, html.Length - start));
            return JsonDocument.ParseValue(ref reader);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetPath(JsonElement element, out JsonElement result, params string[] path)
    {
        result = element;
        foreach (var name in path)
        {
            if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty(name, out result))
                return false;
        }

        return true;
    }

    private static int? GetInt(JsonElement element, string name) =>
        GetDecimal(element, name) is { } value && value == Math.Floor(value) && value is >= int.MinValue and <= int.MaxValue
            ? (int)value
            : null;

    private static decimal? GetDecimal(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var prop))
            return null;

        return prop.ValueKind switch
        {
            JsonValueKind.Number when prop.TryGetDecimal(out var number) => number,
            JsonValueKind.String when decimal.TryParse(prop.GetString(), NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    internal static bool IsRynekPierwotnyUrl(string url) => IsUrlOnDomain(url, Domain);
}
