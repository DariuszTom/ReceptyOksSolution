using System.Text.Json;
using System.Text.RegularExpressions;
using HomeSeeker.Abstractions;
using HomeSeeker.Configuration;
using HomeSeeker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static HomeSeeker.Scrapers.ScraperHelper;

namespace HomeSeeker.Scrapers;

/// <summary>
/// Scraper for Otodom.pl using __NEXT_DATA__ JSON parsing.
/// </summary>
public sealed partial class OtodomScraper : ListingScraperBase
{
    public OtodomScraper(
        IHttpClientFactory httpClientFactory,
        IOptions<HomeSeekerOptions> options,
        ILogger<OtodomScraper> logger)
        : base(httpClientFactory, options, logger)
    {
    }

    public override string PortalName => PortalNames.Otodom;

    protected override string PortalDomain => "otodom.pl";

    protected override IReadOnlyList<ScrapedListing> ParseSearchPage(string html) => ParseNextData(html);

    protected override ListingDetails? ParseDetailsPage(string html) =>
        CreateDetails(ParseDetails(html), ParseCoordinates(html));

    protected override string BuildSearchUrl(SearchProfile profile, PropertyTypes type, int page)
    {
        var city = ScraperHelper.NormalizeCity(profile.City);
        var category = type == PropertyTypes.Apartment ? "mieszkanie" : "dom";
        // "/pl/wyniki/..." now requires the full location path (e.g. malopolskie/krakow/krakow/krakow)
        // and returns 404 for a bare city slug. "/pl/oferty/..." resolves the slug and redirects there,
        // preserving the query string.
        var url = $"https://www.otodom.pl/pl/oferty/sprzedaz/{category}/{city}?page={page}";

        if (profile.MinPrice.HasValue)
            url += $"&priceMin={profile.MinPrice.Value:F0}";
        if (profile.MaxPrice.HasValue)
            url += $"&priceMax={profile.MaxPrice.Value:F0}";
        if (profile.MinAreaSqm.HasValue)
            url += $"&areaMin={profile.MinAreaSqm.Value:F0}";
        if (profile.MaxAreaSqm.HasValue)
            url += $"&areaMax={profile.MaxAreaSqm.Value:F0}";
        if (profile.MinRooms is > 1 and var minRooms)
            url += $"&roomsNumber=%5B{string.Join("%2C", RoomCodesFrom(RoomCodes, minRooms))}%5D";

        return url;
    }

    /// <summary>
    /// Parses the __NEXT_DATA__ JSON from the HTML.
    /// Made internal static for unit testing with fixture HTML.
    /// </summary>
    internal static List<ScrapedListing> ParseNextData(string html)
    {
        var results = new List<ScrapedListing>();

        try
        {
            var match = NextDataRegex().Match(html);
            if (!match.Success)
                return results;

            var json = match.Groups[1].Value;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Navigate to the listings array
            if (!root.TryGetProperty("props", out var props) ||
                !props.TryGetProperty("pageProps", out var pageProps) ||
                !pageProps.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("searchAds", out var searchAds) ||
                !searchAds.TryGetProperty("items", out var items))
            {
                return results;
            }

            foreach (var item in items.EnumerateArray())
            {
                var listing = ParseListing(item);
                if (listing is not null)
                {
                    results.Add(listing);
                }
            }
        }
        catch (JsonException)
        {
            // Malformed JSON - return empty
        }

        return results;
    }

    private static ScrapedListing? ParseListing(JsonElement item)
    {
        try
        {
            // Extract ID
            if (!item.TryGetProperty("id", out var idProp))
                return null;
            var externalId = idProp.ToString();

            // Extract slug for URL
            if (!item.TryGetProperty("slug", out var slugProp))
                return null;
            var slug = slugProp.GetString() ?? string.Empty;
            var url = $"https://www.otodom.pl/pl/oferta/{slug}";

            // Extract title
            if (!item.TryGetProperty("title", out var titleProp))
                return null;
            var title = titleProp.GetString() ?? string.Empty;

            // Extract price
            decimal price = 0;
            if (item.TryGetProperty("totalPrice", out var totalPrice) &&
                totalPrice.TryGetProperty("value", out var priceValue))
            {
                price = priceValue.GetDecimal();
            }

            // Extract area
            decimal area = 0;
            if (item.TryGetProperty("areaInSquareMeters", out var areaProp))
            {
                area = areaProp.GetDecimal();
            }

            // Extract location
            string? location = null;
            if (item.TryGetProperty("location", out var locationProp) &&
                locationProp.ValueKind == JsonValueKind.Object)
            {
                string? city = null;
                string? district = null;

                if (locationProp.TryGetProperty("address", out var addressProp) &&
                    addressProp.ValueKind == JsonValueKind.Object)
                {
                    city = GetNestedName(addressProp, "city");
                    district = GetNestedName(addressProp, "district");
                }

                // Current Otodom format leaves address.city null and exposes the hierarchy here.
                if (city is null &&
                    locationProp.TryGetProperty("reverseGeocoding", out var geo) &&
                    geo.ValueKind == JsonValueKind.Object &&
                    geo.TryGetProperty("locations", out var geoLocations) &&
                    geoLocations.ValueKind == JsonValueKind.Array)
                {
                    foreach (var loc in geoLocations.EnumerateArray())
                    {
                        if (loc.ValueKind != JsonValueKind.Object ||
                            !loc.TryGetProperty("locationLevel", out var level) ||
                            level.ValueKind != JsonValueKind.String)
                        {
                            continue;
                        }

                        var levelName = level.GetString();
                        if (levelName == "city_or_village")
                            city ??= GetString(loc, "name");
                        else if (levelName == "district")
                            district ??= GetString(loc, "name");
                    }
                }

                if (!string.IsNullOrWhiteSpace(city))
                {
                    location = string.IsNullOrWhiteSpace(district) ? city : $"{city}, {district}";
                }
            }

            if (string.IsNullOrWhiteSpace(externalId) || price <= 0 || area <= 0)
                return null;

            return new ScrapedListing
            {
                Portal = PortalNames.Otodom,
                ExternalId = externalId,
                Url = url,
                Title = title,
                Price = price,
                AreaSqm = area,
                Location = location,
                Rooms = ParseRooms(item),
                DevelopmentKey = ParseDevelopmentKey(item)
            };
        }
        catch
        {
            return null;
        }
    }

    // Otodom's roomsNumber enum; "MORE" means more than ten.
    private static readonly string[] RoomCodes =
        ["ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE", "TEN", "MORE"];

    // Units of a developer's investment share "developmentId"; 0 means a standalone offer.
    internal static string? ParseDevelopmentKey(JsonElement item) =>
        item.TryGetProperty("developmentId", out var id) &&
        id.ValueKind == JsonValueKind.Number &&
        id.TryGetInt64(out var value) && value > 0
            ? $"otodom:{value}"
            : null;

    private static int? ParseRooms(JsonElement item)
    {
        if (!item.TryGetProperty("roomsNumber", out var rooms))
            return null;

        if (rooms.ValueKind == JsonValueKind.Number && rooms.TryGetInt32(out var number))
            return number > 0 ? number : null;

        if (rooms.ValueKind != JsonValueKind.String)
            return null;

        // "MORE" is open-ended (> 10), so its exact count is unknown.
        return RoomCountFromCode(RoomCodes, rooms.GetString());
    }

    internal static bool IsOtodomUrl(string url) => IsUrlOnDomain(url, "otodom.pl");

    // Already present in the prompt (price, area) or irrelevant for evaluation.
    private static readonly HashSet<string> SkippedDetailKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "price", "price_per_m", "m", "is_ai_video_enabled", "advertiser_type"
    };

    private static readonly Dictionary<string, string> DetailLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["rent"] = "Czynsz",
        ["market"] = "Rynek",
        ["building_type"] = "Rodzaj zabudowy",
        ["floor_no"] = "Piętro",
        ["building_floors_num"] = "Liczba pięter w budynku",
        ["construction_status"] = "Stan wykończenia",
        ["building_material"] = "Materiał budynku",
        ["windows_type"] = "Okna",
        ["building_ownership"] = "Forma własności",
        ["heating"] = "Ogrzewanie",
        ["rooms_num"] = "Liczba pokoi",
        ["build_year"] = "Rok budowy",
        ["terrain_area"] = "Powierzchnia działki",
        ["security_types"] = "Zabezpieczenia",
        ["extras_types"] = "Dodatkowe",
        ["equipment_types"] = "Wyposażenie",
        ["media_types"] = "Media",
        ["lift"] = "Winda",
        ["roof_type"] = "Dach",
        ["recreational"] = "Rekreacyjny",
        ["location"] = "Położenie",
        ["vicinity_types"] = "Okolica",
        ["fence_types"] = "Ogrodzenie",
        ["access_types"] = "Dojazd",
        ["garret_type"] = "Poddasze",
        ["free_from"] = "Dostępne od"
    };

    /// <summary>
    /// Parses parameters and description from an Otodom ad page
    /// (__NEXT_DATA__ → props.pageProps.ad).
    /// </summary>
    internal static string? ParseDetails(string html)
    {
        try
        {
            var match = NextDataRegex().Match(html);
            if (!match.Success)
                return null;

            using var doc = JsonDocument.Parse(match.Groups[1].Value);
            if (!doc.RootElement.TryGetProperty("props", out var props) ||
                !props.TryGetProperty("pageProps", out var pageProps) ||
                !pageProps.TryGetProperty("ad", out var ad) ||
                ad.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var parameters = new List<(string, string)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (ad.TryGetProperty("characteristics", out var characteristics) &&
                characteristics.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in characteristics.EnumerateArray())
                {
                    if (c.ValueKind != JsonValueKind.Object) continue;
                    var key = GetString(c, "key");
                    if (key is null || SkippedDetailKeys.Contains(key) || !seen.Add(key)) continue;

                    var localized = GetString(c, "localizedValue");
                    var value = string.IsNullOrWhiteSpace(localized) ? HumanizeCode(GetString(c, "value")) : localized;
                    if (!string.IsNullOrWhiteSpace(value))
                        parameters.Add((LabelFor(key), value));
                }
            }

            // Multi-value features (extras, equipment, security, media...) only live here.
            if (ad.TryGetProperty("additionalInformation", out var additional) &&
                additional.ValueKind == JsonValueKind.Array)
            {
                foreach (var info in additional.EnumerateArray())
                {
                    if (info.ValueKind != JsonValueKind.Object) continue;
                    var key = GetString(info, "label");
                    if (key is null || SkippedDetailKeys.Contains(key) || !seen.Add(key)) continue;
                    if (!info.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array) continue;

                    var items = values.EnumerateArray()
                        .Where(v => v.ValueKind == JsonValueKind.String)
                        .Select(v => HumanizeCode(v.GetString()))
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .ToList();
                    if (items.Count > 0)
                        parameters.Add((LabelFor(key), string.Join(", ", items)));
                }
            }

            return ListingDetailsText.Build(parameters, GetString(ad, "description"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads coordinates from an Otodom ad page
    /// (__NEXT_DATA__ → props.pageProps.ad.location.coordinates).
    /// </summary>
    internal static GeoPoint? ParseCoordinates(string html)
    {
        try
        {
            var match = NextDataRegex().Match(html);
            if (!match.Success)
                return null;

            using var doc = JsonDocument.Parse(match.Groups[1].Value);
            if (!doc.RootElement.TryGetProperty("props", out var props) ||
                !props.TryGetProperty("pageProps", out var pageProps) ||
                !pageProps.TryGetProperty("ad", out var ad) || ad.ValueKind != JsonValueKind.Object ||
                !ad.TryGetProperty("location", out var location) || location.ValueKind != JsonValueKind.Object ||
                !location.TryGetProperty("coordinates", out var coords) || coords.ValueKind != JsonValueKind.Object ||
                !coords.TryGetProperty("latitude", out var lat) || lat.ValueKind != JsonValueKind.Number ||
                !coords.TryGetProperty("longitude", out var lon) || lon.ValueKind != JsonValueKind.Number)
            {
                return null;
            }

            // Sellers can hide the exact address; Otodom then exposes a map radius.
            var approximate = location.TryGetProperty("mapDetails", out var mapDetails) &&
                              mapDetails.ValueKind == JsonValueKind.Object &&
                              mapDetails.TryGetProperty("radius", out var radius) &&
                              radius.ValueKind == JsonValueKind.Number && radius.GetDouble() > 0;
            var point = new GeoPoint(lat.GetDouble(), lon.GetDouble(), approximate);
            return point.IsValid ? point : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string LabelFor(string key) =>
        DetailLabels.TryGetValue(key, out var label) ? label : HumanizeCode(key) ?? key;

    /// <summary>
    /// Turns Otodom codes like "extras_types::balcony", "floor_no::floor_1" or "::y" into readable text.
    /// </summary>
    private static string? HumanizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        var sep = code.LastIndexOf("::", StringComparison.Ordinal);
        var value = sep >= 0 ? code[(sep + 2)..] : code;

        return value switch
        {
            "y" => "tak",
            "n" => "nie",
            _ => value.Replace('_', ' ')
        };
    }

    [GeneratedRegex(@"<script\s+id=""__NEXT_DATA__""\s+type=""application/json""[^>]*>(.*?)</script>", RegexOptions.Singleline)]
    private static partial Regex NextDataRegex();
}
