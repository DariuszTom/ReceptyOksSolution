using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using HomeSeeker.Abstractions;
using HomeSeeker.Configuration;
using HomeSeeker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

    public async Task<IReadOnlyList<ScrapedListing>> SearchAsync(
        SearchProfile profile,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ScrapedListing>();

        try
        {
            for (int page = 1; page <= _options.MaxSearchPagesPerPortal; page++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var url = BuildSearchUrl(profile, page);
                _logger.LogDebug("Fetching OLX page {Page}: {Url}", page, url);

                try
                {
                    var html = await _httpClient.GetStringAsync(url, cancellationToken).ConfigureAwait(false);

                    // Try JSON parsing first, fallback to HTML
                    var listings = ParsePrerenderedState(html);
                    if (listings.Count == 0)
                    {
                        _logger.LogDebug("JSON parsing returned empty, trying HTML fallback");
                        listings = ParseHtmlFallback(html);
                    }

                    if (listings.Count == 0)
                    {
                        _logger.LogDebug("No more listings found on page {Page}", page);
                        break;
                    }

                    results.AddRange(listings);

                    if (page < _options.MaxSearchPagesPerPortal)
                    {
                        await Task.Delay(_options.RequestDelay, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _logger.LogWarning("OLX returned 403 Forbidden - possible rate limiting or bot detection");
                    break;
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch OLX page {Page}", page);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("OLX scraping cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during OLX scraping");
        }

        _logger.LogInformation("OLX scraper found {Count} listings for profile {ProfileId}",
            results.Count, profile.Id);

        return results;
    }

    private static string BuildSearchUrl(SearchProfile profile, int page)
    {
        var city = NormalizeCity(profile.City);
        var url = $"https://www.olx.pl/nieruchomosci/domy/sprzedaz/{city}/?page={page}";

        if (profile.MinPrice.HasValue)
            url += $"&search[filter_float_price:from]={profile.MinPrice.Value:F0}";
        if (profile.MaxPrice.HasValue)
            url += $"&search[filter_float_price:to]={profile.MaxPrice.Value:F0}";
        if (profile.MinAreaSqm.HasValue)
            url += $"&search[filter_float_m:from]={profile.MinAreaSqm.Value:F0}";
        if (profile.MaxAreaSqm.HasValue)
            url += $"&search[filter_float_m:to]={profile.MaxAreaSqm.Value:F0}";

        return url;
    }

    private static string NormalizeCity(string city)
    {
        return city.ToLowerInvariant()
            .Replace("ą", "a")
            .Replace("ć", "c")
            .Replace("ę", "e")
            .Replace("ł", "l")
            .Replace("ń", "n")
            .Replace("ó", "o")
            .Replace("ś", "s")
            .Replace("ź", "z")
            .Replace("ż", "z")
            .Replace(" ", "-");
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
        catch (JsonException)
        {
            // Malformed JSON - return empty
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
                    if (param.TryGetProperty("key", out var key) &&
                        key.GetString() == "m" &&
                        param.TryGetProperty("value", out var valueObj) &&
                        valueObj.TryGetProperty("key", out var areaValue))
                    {
                        var areaStr = areaValue.GetString() ?? "0";
                        decimal.TryParse(areaStr, out area);
                        break;
                    }
                }
            }

            // Extract location
            string? location = null;
            if (item.TryGetProperty("location", out var locationProp) &&
                locationProp.TryGetProperty("city", out var cityProp) &&
                cityProp.TryGetProperty("name", out var cityName))
            {
                location = cityName.GetString();

                if (locationProp.TryGetProperty("district", out var districtProp) &&
                    districtProp.TryGetProperty("name", out var districtName))
                {
                    location = $"{location}, {districtName.GetString()}";
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
                Location = location
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

            // Extract ID from URL
            var idMatch = IdFromUrlRegex().Match(href);
            var externalId = idMatch.Success ? idMatch.Groups[1].Value : href.GetHashCode().ToString();

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
                    if (areaMatch.Success && decimal.TryParse(areaMatch.Groups[1].Value, out var parsedArea))
                    {
                        area = parsedArea;
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
}
