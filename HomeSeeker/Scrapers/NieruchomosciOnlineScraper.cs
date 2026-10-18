using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using HomeSeeker.Configuration;
using HomeSeeker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static HomeSeeker.Scrapers.ScraperHelper;

namespace HomeSeeker.Scrapers;

/// <summary>
/// Scraper for nieruchomosci-online.pl. Search pages embed every tile's data in the
/// <c>NOTrafficEventBuilder.buildCachedProps({...})</c> JSON (price, area, rooms, market);
/// URL and title come from the matching HTML tile. Ad pages provide JSON-LD with coordinates.
/// </summary>
public sealed partial class NieruchomosciOnlineScraper : ListingScraperBase
{
    private const string BaseUrl = "https://www.nieruchomosci-online.pl";

    public NieruchomosciOnlineScraper(
        IHttpClientFactory httpClientFactory,
        IOptions<HomeSeekerOptions> options,
        ILogger<NieruchomosciOnlineScraper> logger)
        : base(httpClientFactory, options, logger)
    {
    }

    public override string PortalName => PortalNames.NieruchomosciOnline;

    protected override string PortalDomain => "nieruchomosci-online.pl";

    protected override IReadOnlyList<ScrapedListing> ParseSearchPage(string html) => ParseSearchResults(html);

    protected override ListingDetails? ParseDetailsPage(string html) =>
        CreateDetails(ParseDetails(html), ParseCoordinates(html));

    /// <summary>
    /// Search URL format: <c>szukaj.html?3,{type},sprzedaz,,{City},,,,{priceFrom}-{priceTo},{areaFrom}-{areaTo}&amp;p={page}</c>;
    /// open-ended ranges are allowed ("-900000", "90-") and an empty field means no filter.
    /// </summary>
    protected override string BuildSearchUrl(SearchProfile profile, PropertyTypes type, int page)
    {
        var category = type == PropertyTypes.Apartment ? "mieszkanie" : "dom";
        var city = Uri.EscapeDataString(profile.City.Trim());
        var price = Range(profile.MinPrice, profile.MaxPrice);
        var area = Range(profile.MinAreaSqm, profile.MaxAreaSqm);

        var url = $"{BaseUrl}/szukaj.html?3,{category},sprzedaz,,{city},,,,{price},{area}";
        return page > 1 ? $"{url}&p={page}" : url;
    }

    private static string Range(decimal? from, decimal? to) =>
        from is null && to is null
            ? string.Empty
            : $"{from?.ToString("F0", CultureInfo.InvariantCulture)}-{to?.ToString("F0", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Parses a search results page: cached props JSON joined with HTML tiles, falling back to
    /// the tiles alone when the JSON is missing. Internal static for fixture-based unit tests.
    /// </summary>
    internal static List<ScrapedListing> ParseSearchResults(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var tiles = ReadTiles(doc);

        var records = ReadCachedRecords(html);
        if (records is null)
            return [.. tiles.Values.Select(t => t.ToListing()).OfType<ScrapedListing>()];

        var results = new List<ScrapedListing>();
        foreach (var (key, record) in records)
        {
            if (!tiles.TryGetValue(key, out var tile) || tile.Url is null)
                continue;

            var listing = FromRecord(key, record, tile);
            if (listing is not null)
                results.Add(listing);
        }

        return results;
    }

    /// <summary>
    /// Records from <c>record_props</c>, keyed like the tiles' data-id: "a{id}" (regular ad) or
    /// "i{investmentId}_{id}" (unit of a developer's investment). Null when the JSON is missing.
    /// </summary>
    private static List<(string Key, CachedRecord Record)>? ReadCachedRecords(string html)
    {
        var match = CachedPropsRegex().Match(html);
        if (!match.Success)
            return null;

        try
        {
            using var json = JsonDocument.Parse(match.Groups[1].Value);
            if (!json.RootElement.TryGetProperty("record_props", out var props) ||
                props.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var records = new List<(string, CachedRecord)>();
            foreach (var prop in props.EnumerateObject())
            {
                var r = prop.Value;
                if (r.ValueKind != JsonValueKind.Object)
                    continue;

                records.Add((prop.Name, new CachedRecord(
                    Transaction: GetString(r, "rctrn"),
                    Price: GetDecimal(r, "prc"),
                    Area: GetDecimal(r, "rsur"),
                    Rooms: (int?)GetDecimal(r, "rooms"),
                    Market: GetString(r, "rcmrkt"),
                    Street: GetString(r, "rlocstsn"))));
            }

            return records;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ScrapedListing? FromRecord(string key, CachedRecord record, Tile tile)
    {
        if (record.Transaction is not null && record.Transaction != "sale")
            return null;

        var (externalId, developmentKey) = ParseKey(key);
        var price = record.Price ?? tile.Price ?? 0;
        var area = record.Area ?? tile.Area ?? 0;
        if (externalId is null || price <= 0 || area <= 0)
            return null;

        return new ScrapedListing
        {
            Portal = PortalNames.NieruchomosciOnline,
            ExternalId = externalId,
            Url = tile.Url!,
            Title = tile.Title ?? "Ogłoszenie",
            Price = price,
            AreaSqm = area,
            Location = BuildLocation(tile.Province, record.Street),
            Rooms = record.Rooms is > 0 ? record.Rooms : null,
            DevelopmentKey = developmentKey,
            Details = record.Market == "primary" ? "- Rynek: pierwotny" : null
        };
    }

    /// <summary>"a123" → ("123", null); "i456_123" → ("123", "nieruchomosci-online:456").</summary>
    internal static (string? ExternalId, string? DevelopmentKey) ParseKey(string key)
    {
        var match = TileKeyRegex().Match(key);
        if (!match.Success)
            return (null, null);

        var investment = match.Groups["inv"];
        return (match.Groups["id"].Value,
            investment.Success ? $"nieruchomosci-online:{investment.Value}" : null);
    }

    private static string? BuildLocation(string? province, string? street)
    {
        if (string.IsNullOrWhiteSpace(province))
            return string.IsNullOrWhiteSpace(street) ? null : street;
        return string.IsNullOrWhiteSpace(street) ? province : $"{province}, {street}";
    }

    private static Dictionary<string, Tile> ReadTiles(HtmlDocument doc)
    {
        var tiles = new Dictionary<string, Tile>(StringComparer.Ordinal);
        var nodes = doc.DocumentNode.SelectNodes("//div[contains(concat(' ', normalize-space(@class), ' '), ' tile ') and @data-id]");
        if (nodes is null)
            return tiles;

        foreach (var node in nodes)
        {
            var key = node.GetAttributeValue("data-id", string.Empty);
            if (key.Length == 0 || tiles.ContainsKey(key))
                continue;

            var link = node.SelectSingleNode(".//h2[contains(@class, 'name')]//a[@href]");
            var display = node.SelectSingleNode(".//p[contains(@class, 'primary-display')]");
            var province = node.SelectSingleNode(".//p[contains(@class, 'province')]");

            tiles[key] = new Tile(
                key,
                NormalizeAdUrl(link?.GetAttributeValue("href", null)),
                CleanText(link?.InnerText),
                CleanText(JoinText(province)),
                ParseNumber(display?.SelectSingleNode("./span[not(@class)]")?.InnerText),
                ParseNumber(display?.SelectSingleNode("./span[contains(@class, 'area')]")?.InnerText));
        }

        return tiles;
    }

    /// <summary>Strips the search context query (e.g. "?3,mieszkanie,...") so the URL is stable.</summary>
    internal static string? NormalizeAdUrl(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
            return null;

        var url = HtmlEntity.DeEntitize(href.Trim());
        var query = url.IndexOfAny(['?', '#']);
        if (query >= 0)
            url = url[..query];
        if (url.StartsWith('/'))
            url = BaseUrl + url;

        return IsUrlOnDomain(url, "nieruchomosci-online.pl") ? url : null;
    }

    /// <summary>Text of all descendant text nodes separated by spaces ("&lt;a&gt;Bieńczyce,&lt;/a&gt;&lt;span&gt;Kraków&lt;/span&gt;").</summary>
    private static string? JoinText(HtmlNode? node) =>
        node is null
            ? null
            : string.Join(' ', node.DescendantsAndSelf().Where(n => n.NodeType == HtmlNodeType.Text).Select(n => n.InnerText));

    private static string? CleanText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var cleaned = WhitespaceRegex().Replace(HtmlEntity.DeEntitize(text), " ").Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }

    /// <summary>Parses "702&amp;nbsp;600&amp;nbsp;zł" or "69,02&amp;nbsp;m²"; null when no number.</summary>
    internal static decimal? ParseNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var match = NumberRegex().Match(HtmlEntity.DeEntitize(text).Replace('\u00A0', ' '));
        if (!match.Success)
            return null;

        var digits = match.Value.Replace(" ", string.Empty).Replace(',', '.');
        return decimal.TryParse(digits, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

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

    internal static bool IsNieruchomosciOnlineUrl(string url) => IsUrlOnDomain(url, "nieruchomosci-online.pl");

    /// <summary>
    /// Parameters (JSON-LD additionalProperty, amenityFeature) and the full description
    /// (<c>#boxCustomDesc .estate-desc-more</c>) from an ad page.
    /// </summary>
    internal static string? ParseDetails(string html)
    {
        var parameters = new List<(string, string)>();
        string? description = null;

        var offer = FindOfferJsonLd(html);
        if (offer is { } ld)
        {
            using (ld)
            {
                var root = ld.RootElement;
                if (root.TryGetProperty("additionalProperty", out var props) && props.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in props.EnumerateArray())
                    {
                        if (p.ValueKind != JsonValueKind.Object)
                            continue;
                        var name = GetString(p, "name");
                        var value = GetString(p, "value");
                        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
                            parameters.Add((name, value));
                    }
                }

                if (root.TryGetProperty("amenityFeature", out var amenities) && amenities.ValueKind == JsonValueKind.Array)
                {
                    var names = amenities.EnumerateArray()
                        .Where(a => a.ValueKind == JsonValueKind.Object &&
                                    a.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.True)
                        .Select(a => GetString(a, "name"))
                        .OfType<string>()
                        .ToList();
                    if (names.Count > 0)
                        parameters.Add(("Amenities", string.Join(", ", names)));
                }

                description = GetString(root, "description");
            }
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var descNode = doc.DocumentNode.SelectSingleNode("//div[@id='boxCustomDesc']//div[contains(@class, 'estate-desc-more')]")
                       ?? doc.DocumentNode.SelectSingleNode("//div[@id='boxCustomDesc']");
        if (descNode is not null && !string.IsNullOrWhiteSpace(descNode.InnerText))
            description = descNode.InnerHtml;

        return ListingDetailsText.Build(parameters, description);
    }

    /// <summary>Reads JSON-LD <c>geo</c> (latitude/longitude as strings or numbers) from an ad page.</summary>
    internal static GeoPoint? ParseCoordinates(string html)
    {
        using var ld = FindOfferJsonLd(html);
        if (ld is null ||
            !ld.RootElement.TryGetProperty("geo", out var geo) || geo.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var lat = GetDecimal(geo, "latitude");
        var lon = GetDecimal(geo, "longitude");
        if (lat is null || lon is null)
            return null;

        var point = new GeoPoint((double)lat.Value, (double)lon.Value);
        return point.IsValid ? point : null;
    }

    /// <summary>The JSON-LD block describing the property (the one with "geo" or "additionalProperty").</summary>
    private static JsonDocument? FindOfferJsonLd(string html)
    {
        foreach (Match match in JsonLdRegex().Matches(html))
        {
            try
            {
                var doc = JsonDocument.Parse(match.Groups[1].Value);
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    (doc.RootElement.TryGetProperty("geo", out _) ||
                     doc.RootElement.TryGetProperty("additionalProperty", out _)))
                {
                    return doc;
                }

                doc.Dispose();
            }
            catch (JsonException)
            {
            }
        }

        return null;
    }

    private sealed record CachedRecord(
        string? Transaction, decimal? Price, decimal? Area, int? Rooms,
        string? Market, string? Street);

    private sealed record Tile(string Key, string? Url, string? Title, string? Province, decimal? Price, decimal? Area)
    {
        public ScrapedListing? ToListing()
        {
            var (externalId, developmentKey) = ParseKey(Key);
            if (externalId is null || Url is null || Price is not > 0 || Area is not > 0)
                return null;

            return new ScrapedListing
            {
                Portal = PortalNames.NieruchomosciOnline,
                ExternalId = externalId,
                Url = Url,
                Title = Title ?? "Ogłoszenie",
                Price = Price.Value,
                AreaSqm = Area.Value,
                Location = Province,
                DevelopmentKey = developmentKey
            };
        }
    }

    [GeneratedRegex(@"buildCachedProps\((\{.*?\})\);", RegexOptions.Singleline)]
    private static partial Regex CachedPropsRegex();

    [GeneratedRegex(@"^(?:a|i(?<inv>\d+)_)(?<id>\d+)$")]
    private static partial Regex TileKeyRegex();

    [GeneratedRegex(@"<script[^>]*type=""application/ld\+json""[^>]*>(.*?)</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex JsonLdRegex();

    [GeneratedRegex(@"\d[\d ]*(?:,\d+)?")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
