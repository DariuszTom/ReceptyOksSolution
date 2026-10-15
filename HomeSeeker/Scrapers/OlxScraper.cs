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
public sealed partial class OlxScraper : IListingScraper
{
    private readonly HttpClient _httpClient;
    private readonly HomeSeekerOptions _options;
    private readonly ILogger<OlxScraper> _logger;

    public string PortalName => "OLX";

    public OlxScraper(
        IHttpClientFactory httpClientFactory,
        IOptions<HomeSeekerOptions> options,
        ILogger<OlxScraper> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        _httpClient = httpClientFactory.CreateClient("homeseeker-scraper");
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ScrapeResult> SearchAsync(
        SearchProfile profile,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ScrapedListing>();
        var pagesFetched = 0;
        string? failure = null;
        var lostTypes = new List<PropertyTypes>();
        var partialTypes = new List<(PropertyTypes Type, string Error)>();

        try
        {
            var requestsSent = 0;
            var blocked = false;
            foreach (var type in profile.PropertyTypes.Selected())
            {
                // Bot detection applies to the whole portal; do not hammer it with the next category.
                if (blocked)
                {
                    lostTypes.Add(type);
                    continue;
                }

                var pagesBefore = pagesFetched;
                string? typeFailure = null;
                for (int page = 1; page <= _options.MaxSearchPagesPerPortal; page++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (requestsSent++ > 0)
                    {
                        await Task.Delay(_options.RequestDelay, cancellationToken).ConfigureAwait(false);
                    }

                    var url = BuildSearchUrl(profile, type, page);
                    _logger.LogDebug("Fetching OLX {Type} page {Page}: {Url}", type, page, url);

                    try
                    {
                        var html = await _httpClient.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
                        pagesFetched++;

                        // Try JSON parsing first, fallback to HTML
                        var listings = ParsePrerenderedState(html);
                        if (listings.Count == 0)
                        {
                            _logger.LogDebug("JSON parsing returned empty, trying HTML fallback");
                            listings = ParseHtmlFallback(html);
                        }

                        if (listings.Count == 0)
                        {
                            _logger.LogDebug("No more {Type} listings found on page {Page}", type, page);
                            break;
                        }

                        results.AddRange(listings.Select(l => l with { PropertyType = type }));
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        _logger.LogWarning("OLX returned 403 Forbidden - possible rate limiting or bot detection");
                        failure = "OLX returned 403 Forbidden";
                        typeFailure = failure;
                        blocked = true;
                        break;
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch OLX {Type} page {Page}", type, page);
                        failure = $"OLX request failed: {ex.Message}";
                        typeFailure = failure;
                        break;
                    }
                }

                if (pagesFetched == pagesBefore && !cancellationToken.IsCancellationRequested)
                    lostTypes.Add(type);
                else if (typeFailure is not null)
                    partialTypes.Add((type, typeFailure)); // Later page failed: keep the listings, flag as incomplete.
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("OLX scraping cancelled");
            failure = "OLX scraping cancelled";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during OLX scraping");
            failure = $"OLX scraping error: {ex.Message}";
        }

        // Only treat the portal as failed when we never got a usable page.
        // A failure after at least one good page still yields a partial success.
        if (pagesFetched == 0 && failure is not null)
        {
            return ScrapeResult.Failure(failure);
        }

        _logger.LogInformation("OLX scraper found {Count} listings for profile {ProfileId}",
            results.Count, profile.Id);

        var warnings = lostTypes.Select(t => $"{t.ToPolish()} - nie pobrano")
            .Concat(partialTypes.Select(p => $"{p.Type.ToPolish()} - niepełne wyniki ({p.Error})"))
            .ToList();
        return ScrapeResult.Success(results, warnings);
    }

    private static string BuildSearchUrl(SearchProfile profile, PropertyTypes type, int page)
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
                Portal = "OLX",
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
                Portal = "OLX",
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

    public async Task<ListingDetails?> GetDetailsAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!IsOlxUrl(url))
            return null;

        try
        {
            var html = await _httpClient.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            var text = ParseDetails(html);
            var coordinates = ParseCoordinates(html);
            return text is null && coordinates is null ? null : new ListingDetails(text, coordinates);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch OLX listing details {Url}", url);
            return null;
        }
    }

    internal static bool IsOlxUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        (uri.Host.Equals("olx.pl", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.EndsWith(".olx.pl", StringComparison.OrdinalIgnoreCase));

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
