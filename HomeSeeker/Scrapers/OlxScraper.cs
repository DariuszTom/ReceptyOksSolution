using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using HomeSeeker.Abstractions;
using HomeSeeker.Configuration;
using HomeSeeker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static HomeSeeker.Scrapers.ScraperHelper;

namespace HomeSeeker.Scrapers;

/// <summary>
/// Scraper for OLX.pl using __PRERENDERED_STATE__ JSON parsing with HtmlAgilityPack fallback.
/// </summary>
public sealed partial class OlxScraper : ListingScraperBase
{
    public OlxScraper(
        IHttpClientFactory httpClientFactory,
        IOptions<HomeSeekerOptions> options,
        ILogger<OlxScraper> logger)
        : base(httpClientFactory, options, logger)
    {
    }

    public override string PortalName => PortalNames.Olx;

    protected override string PortalDomain => "olx.pl";

    protected override IReadOnlyList<ScrapedListing> ParseSearchPage(string html)
    {
        // Try JSON parsing first, fallback to HTML
        var listings = ParsePrerenderedState(html);
        if (listings.Count > 0)
            return listings;

        Logger.LogDebug("JSON parsing returned empty, trying HTML fallback");
        return ParseHtmlFallback(html);
    }

    protected override ListingDetails? ParseDetailsPage(string html) =>
        CreateDetails(ParseDetails(html), ParseCoordinates(html));

    protected override string BuildSearchUrl(SearchProfile profile, PropertyTypes type, int page)
    {
        var city = ScraperHelper.NormalizeCity(profile.City);
        var category = type == PropertyTypes.Apartment ? "mieszkania" : "domy";
        var url = $"https://www.olx.pl/nieruchomosci/{category}/sprzedaz/{city}/?page={page}";

        if (profile.MinPrice.HasValue)
            url += $"&search[filter_float_price:from]={profile.MinPrice.Value:F0}";
        if (profile.MaxPrice.HasValue)
            url += $"&search[filter_float_price:to]={profile.MaxPrice.Value:F0}";
        if (profile.MinAreaSqm.HasValue)
            url += $"&search[filter_float_m:from]={profile.MinAreaSqm.Value:F0}";
        if (profile.MaxAreaSqm.HasValue)
            url += $"&search[filter_float_m:to]={profile.MaxAreaSqm.Value:F0}";

        // OLX has a room filter only for apartments; its last option "four" means "4 i więcej".
        if (type == PropertyTypes.Apartment && profile.MinRooms is > 1 and var minRooms)
        {
            var codes = RoomCodesFrom(OlxRoomCodes, minRooms).ToList();
            for (var i = 0; i < codes.Count; i++)
                url += $"&search[filter_enum_rooms][{i}]={codes[i]}";
        }

        return url;
    }

    /// <summary>
    /// Parses the __PRERENDERED_STATE__ JSON from the HTML.
    /// Made internal static for unit testing with fixture HTML.
    /// </summary>
    internal static List<ScrapedListing> ParsePrerenderedState(string html)
    {
        var results = new List<ScrapedListing>();

        try
        {
            var match = PrerenderedStateRegex().Match(html);
            if (!match.Success)
                return results;

            var json = match.Groups[1].Value;

            // Unescape the JSON string (OLX often escapes it)
            json = Regex.Unescape(json);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Navigate to the listings - OLX structure varies, try multiple paths
            JsonElement? itemsArray = null;

            if (root.TryGetProperty("listing", out var listing) &&
                listing.TryGetProperty("listing", out var listingData) &&
                listingData.TryGetProperty("ads", out var ads))
            {
                itemsArray = ads;
            }
            else if (root.TryGetProperty("ads", out var adsRoot))
            {
                itemsArray = adsRoot;
            }

            if (itemsArray is null)
                return results;

            foreach (var item in itemsArray.Value.EnumerateArray())
            {
                var listingResult = ParseJsonListing(item);
                if (listingResult is not null)
                {
                    results.Add(listingResult);
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            // Malformed JSON or invalid escape sequence - return empty
        }

        return results;
    }

    private static ScrapedListing? ParseJsonListing(JsonElement item)
    {
        try
        {
            // Extract ID
            if (!item.TryGetProperty("id", out var idProp))
                return null;
            var externalId = idProp.ToString();

            // Extract URL
            string url;
            if (item.TryGetProperty("url", out var urlProp))
            {
                url = urlProp.GetString() ?? string.Empty;
                if (!url.StartsWith("http"))
                    url = $"https://www.olx.pl{url}";
            }
            else
            {
                return null;
            }

            // Extract title
            if (!item.TryGetProperty("title", out var titleProp))
                return null;
            var title = titleProp.GetString() ?? string.Empty;

            // Extract price
            decimal price = 0;
            if (item.TryGetProperty("price", out var priceProp))
            {
                if (priceProp.TryGetProperty("regularPrice", out var regularPrice) &&
                    regularPrice.TryGetProperty("value", out var priceValue))
                {
                    price = priceValue.GetDecimal();
                }
                else if (priceProp.TryGetProperty("displayValue", out var displayValue))
                {
                    var priceStr = displayValue.GetString() ?? "";
                    price = ParsePriceString(priceStr);
                }
            }

            // Extract area from params
            decimal area = 0;
            if (item.TryGetProperty("params", out var paramsProp))
            {
                foreach (var param in paramsProp.EnumerateArray())
                {
                    if (param.ValueKind != JsonValueKind.Object ||
                        !param.TryGetProperty("key", out var key) ||
                        key.ValueKind != JsonValueKind.String ||
                        key.GetString() != "m")
                    {
                        continue;
                    }

                    // Current OLX format: "normalizedValue": "100.2"; legacy: "value": { "key": "100.2" }.
                    string? areaStr = null;
                    if (param.TryGetProperty("normalizedValue", out var normalized) &&
                        normalized.ValueKind == JsonValueKind.String)
                    {
                        areaStr = normalized.GetString();
                    }
                    else if (param.TryGetProperty("value", out var valueProp))
                    {
                        if (valueProp.ValueKind == JsonValueKind.Object &&
                            valueProp.TryGetProperty("key", out var areaValue) &&
                            areaValue.ValueKind == JsonValueKind.String)
                        {
                            areaStr = areaValue.GetString();
                        }
                        else if (valueProp.ValueKind == JsonValueKind.String)
                        {
                            var match = AreaRegex().Match(valueProp.GetString() ?? string.Empty);
                            if (match.Success)
                                areaStr = match.Groups[1].Value;
                        }
                    }

                    area = ParseDecimalInvariant(areaStr ?? "0");
                    break;
                }
            }

            // Extract location
            string? location = null;
            if (item.TryGetProperty("location", out var locationProp) &&
                locationProp.ValueKind == JsonValueKind.Object)
            {
                // Current OLX format uses flat cityName/districtName; legacy uses nested city.name/district.name.
                var city = GetNestedName(locationProp, "city") ?? GetString(locationProp, "cityName");
                var district = GetNestedName(locationProp, "district") ?? GetString(locationProp, "districtName");

                if (!string.IsNullOrWhiteSpace(city))
                {
                    location = string.IsNullOrWhiteSpace(district) ? city : $"{city}, {district}";
                }
            }

            if (string.IsNullOrWhiteSpace(externalId) || price <= 0 || area <= 0)
                return null;

            return new ScrapedListing
            {
                Portal = PortalNames.Olx,
                ExternalId = externalId,
                Url = url,
                Title = title,
                Price = price,
                AreaSqm = area,
                Location = location,
                Details = BuildDetails(item),
                Rooms = ParseRooms(item),
                Coordinates = ParseMap(item)
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// HTML fallback parser using HtmlAgilityPack.
    /// </summary>
    internal static List<ScrapedListing> ParseHtmlFallback(string html)
    {
        var results = new List<ScrapedListing>();

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Find listing cards
            var cards = doc.DocumentNode.SelectNodes("//div[@data-cy='l-card']");
            if (cards is null)
                return results;

            foreach (var card in cards)
            {
                var listing = ParseHtmlCard(card);
                if (listing is not null)
                {
                    results.Add(listing);
                }
            }
        }
        catch
        {
            // HTML parsing failed - return empty
        }

        return results;
    }

    private static ScrapedListing? ParseHtmlCard(HtmlNode card)
    {
        try
        {
            // Extract link and ID
            var linkNode = card.SelectSingleNode(".//a[@href]");
            if (linkNode is null)
                return null;

            var href = linkNode.GetAttributeValue("href", "");
            if (string.IsNullOrWhiteSpace(href))
                return null;

            var url = href.StartsWith("http") ? href : $"https://www.olx.pl{href}";

            // Extract ID from URL; fall back to a deterministic digest so the same
            // listing maps to the same ExternalId across process restarts.
            var idMatch = IdFromUrlRegex().Match(href);
            var externalId = idMatch.Success ? idMatch.Groups[1].Value : ComputeFallbackId(url);

            // Extract title
            var titleNode = card.SelectSingleNode(".//h6") ?? card.SelectSingleNode(".//h4");
            var title = titleNode?.InnerText.Trim() ?? "Unknown";

            // Extract price
            var priceNode = card.SelectSingleNode(".//p[@data-testid='ad-price']") ??
                           card.SelectSingleNode(".//*[contains(@class, 'price')]");
            var priceStr = priceNode?.InnerText.Trim() ?? "0";
            var price = ParsePriceString(priceStr);

            // Extract area (usually in params section)
            decimal area = 0;
            var paramsNodes = card.SelectNodes(".//*[contains(@class, 'params')]//span");
            if (paramsNodes is not null)
            {
                foreach (var param in paramsNodes)
                {
                    var text = param.InnerText.Trim();
                    var areaMatch = AreaRegex().Match(text);
                    if (areaMatch.Success)
                    {
                        area = ParseDecimalInvariant(areaMatch.Groups[1].Value);
                        if (area > 0)
                            break;
                    }
                }
            }

            // Extract location
            var locationNode = card.SelectSingleNode(".//*[@data-testid='location-date']") ??
                              card.SelectSingleNode(".//*[contains(@class, 'location')]");
            var location = locationNode?.InnerText.Split('-')[0].Trim();

            if (price <= 0 || area <= 0)
                return null;

            return new ScrapedListing
            {
                Portal = PortalNames.Olx,
                ExternalId = externalId,
                Url = url,
                Title = HtmlEntity.DeEntitize(title),
                Price = price,
                AreaSqm = area,
                Location = location is not null ? HtmlEntity.DeEntitize(location) : null
            };
        }
        catch
        {
            return null;
        }
    }

    internal static bool IsOlxUrl(string url) => IsUrlOnDomain(url, "olx.pl");

    /// <summary>
    /// Parses parameters and description from an OLX ad page (__PRERENDERED_STATE__ → ad.ad).
    /// </summary>
    internal static string? ParseDetails(string html)
    {
        try
        {
            var match = PrerenderedStateRegex().Match(html);
            if (!match.Success)
                return null;

            using var doc = JsonDocument.Parse(Regex.Unescape(match.Groups[1].Value));
            if (!doc.RootElement.TryGetProperty("ad", out var adWrapper) ||
                adWrapper.ValueKind != JsonValueKind.Object ||
                !adWrapper.TryGetProperty("ad", out var ad) ||
                ad.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return BuildDetails(ad);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads coordinates from an OLX ad page (__PRERENDERED_STATE__ → ad.ad.map).
    /// </summary>
    internal static GeoPoint? ParseCoordinates(string html)
    {
        try
        {
            var match = PrerenderedStateRegex().Match(html);
            if (!match.Success)
                return null;

            using var doc = JsonDocument.Parse(Regex.Unescape(match.Groups[1].Value));
            return doc.RootElement.TryGetProperty("ad", out var adWrapper) &&
                   adWrapper.ValueKind == JsonValueKind.Object &&
                   adWrapper.TryGetProperty("ad", out var ad) &&
                   ad.ValueKind == JsonValueKind.Object
                ? ParseMap(ad)
                : null;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads "map": { "lat", "lon", "radius" }. A positive radius means OLX shows only an
    /// approximate area, not the exact address.
    /// </summary>
    internal static GeoPoint? ParseMap(JsonElement item)
    {
        if (!item.TryGetProperty("map", out var map) || map.ValueKind != JsonValueKind.Object ||
            !map.TryGetProperty("lat", out var lat) || lat.ValueKind != JsonValueKind.Number ||
            !map.TryGetProperty("lon", out var lon) || lon.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        var approximate = map.TryGetProperty("radius", out var radius) &&
                          radius.ValueKind == JsonValueKind.Number && radius.GetDouble() > 0;
        var point = new GeoPoint(lat.GetDouble(), lon.GetDouble(), approximate);
        return point.IsValid ? point : null;
    }

    private static readonly string[] OlxRoomCodes = ["one", "two", "three", "four"];

    /// <summary>
    /// Reads the "rooms" param. OLX's top option ("four" / "4 i więcej") is open-ended,
    /// so it is reported as unknown rather than exactly 4; the URL filter handles it.
    /// </summary>
    internal static int? ParseRooms(JsonElement item)
    {
        if (!item.TryGetProperty("params", out var paramsProp) || paramsProp.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var param in paramsProp.EnumerateArray())
        {
            if (param.ValueKind != JsonValueKind.Object || GetString(param, "key") != "rooms")
                continue;

            var code = GetString(param, "normalizedValue");
            string? text = null;
            if (param.TryGetProperty("value", out var valueProp))
            {
                if (valueProp.ValueKind == JsonValueKind.Object)
                {
                    code ??= GetString(valueProp, "key");
                    text = GetString(valueProp, "label");
                }
                else if (valueProp.ValueKind == JsonValueKind.String)
                {
                    text = valueProp.GetString();
                }
            }

            if (text is not null && text.Contains("więcej", StringComparison.OrdinalIgnoreCase))
                return null;

            if (Array.Exists(OlxRoomCodes, c => c.Equals(code, StringComparison.OrdinalIgnoreCase)))
                return RoomCountFromCode(OlxRoomCodes, code);

            var digits = RoomsDigitsRegex().Match(text ?? code ?? string.Empty);
            return digits.Success && int.TryParse(digits.Value, out var rooms) && rooms > 0 ? rooms : null;
        }

        return null;
    }

    private static string? BuildDetails(JsonElement ad)
    {
        var parameters = new List<(string, string)>();
        if (ad.TryGetProperty("params", out var paramsProp) && paramsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var param in paramsProp.EnumerateArray())
            {
                if (param.ValueKind != JsonValueKind.Object)
                    continue;

                var name = GetString(param, "name");
                var value = GetString(param, "value");
                if (name is not null && value is not null)
                    parameters.Add((name, value));
            }
        }

        return ListingDetailsText.Build(parameters, GetString(ad, "description"));
    }

    private static decimal ParseDecimalInvariant(string value)
    {
        return decimal.TryParse(
            value.Replace(',', '.'),
            System.Globalization.NumberStyles.AllowDecimalPoint,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : 0;
    }

    internal static string ComputeFallbackId(string url)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        return "url-" + Convert.ToHexString(hash, 0, 16).ToLowerInvariant();
    }

    private static decimal ParsePriceString(string priceStr)
    {
        // Remove spaces, "zł", "PLN" and parse
        var cleaned = priceStr
            .Replace(" ", "")
            .Replace("zł", "")
            .Replace("PLN", "")
            .Replace(",", ".")
            .Trim();

        // Keep only digits and decimal point
        cleaned = PriceCleanRegex().Replace(cleaned, "");

        if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var price))
        {
            return price;
        }
        return 0;
    }

    [GeneratedRegex(@"window\.__PRERENDERED_STATE__\s*=\s*""(.+?)""(?:;|<)", RegexOptions.Singleline)]
    private static partial Regex PrerenderedStateRegex();

    [GeneratedRegex(@"ID(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex IdFromUrlRegex();

    [GeneratedRegex(@"(\d+(?:[.,]\d+)?)\s*m")]
    private static partial Regex AreaRegex();

    [GeneratedRegex(@"[^\d.]")]
    private static partial Regex PriceCleanRegex();

    [GeneratedRegex(@"\d+")]
    private static partial Regex RoomsDigitsRegex();
}
