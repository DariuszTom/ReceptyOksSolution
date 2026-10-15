using System.Text.Json;
using System.Text.RegularExpressions;
using HomeSeeker.Abstractions;
using HomeSeeker.Configuration;
using HomeSeeker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HomeSeeker.Scrapers;

/// <summary>
/// Scraper for Otodom.pl using __NEXT_DATA__ JSON parsing.
/// </summary>
public sealed partial class OtodomScraper : IListingScraper
{
    private readonly HttpClient _httpClient;
    private readonly HomeSeekerOptions _options;
    private readonly ILogger<OtodomScraper> _logger;

    public string PortalName => "Otodom";

    public OtodomScraper(
        IHttpClientFactory httpClientFactory,
        IOptions<HomeSeekerOptions> options,
        ILogger<OtodomScraper> logger)
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
                for (int page = 1; page <= _options.MaxSearchPagesPerPortal; page++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (requestsSent++ > 0)
                    {
                        await Task.Delay(_options.RequestDelay, cancellationToken).ConfigureAwait(false);
                    }

                    var url = BuildSearchUrl(profile, type, page);
                    _logger.LogDebug("Fetching Otodom {Type} page {Page}: {Url}", type, page, url);

                    try
                    {
                        var html = await _httpClient.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
                        pagesFetched++;
                        var listings = ParseNextData(html);

                        if (listings.Count == 0)
                        {
                            _logger.LogDebug("No more {Type} listings found on page {Page}", type, page);
                            break;
                        }

                        results.AddRange(listings.Select(l => l with { PropertyType = type }));
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        _logger.LogWarning("Otodom returned 403 Forbidden - possible rate limiting or bot detection");
                        failure = "Otodom returned 403 Forbidden";
                        blocked = true;
                        break;
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch Otodom {Type} page {Page}", type, page);
                        failure = $"Otodom request failed: {ex.Message}";
                        break;
                    }
                }

                if (pagesFetched == pagesBefore && !cancellationToken.IsCancellationRequested)
                    lostTypes.Add(type);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Otodom scraping cancelled");
            failure = "Otodom scraping cancelled";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Otodom scraping");
            failure = $"Otodom scraping error: {ex.Message}";
        }

        // Only treat the portal as failed when we never got a usable page.
        if (pagesFetched == 0 && failure is not null)
        {
            return ScrapeResult.Failure(failure);
        }

        _logger.LogInformation("Otodom scraper found {Count} listings for profile {ProfileId}",
            results.Count, profile.Id);

        return ScrapeResult.Success(results, lostTypes.Select(t => $"{t.ToPolish()} - nie pobrano").ToList());
    }

    private static string BuildSearchUrl(SearchProfile profile, PropertyTypes type, int page)
    {
        var city = NormalizeCity(profile.City);
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
                Portal = "Otodom",
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

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static string? GetNestedName(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Object
            ? GetString(prop, "name")
            : null;

    [GeneratedRegex(@"<script\s+id=""__NEXT_DATA__""\s+type=""application/json""[^>]*>(.*?)</script>", RegexOptions.Singleline)]
    private static partial Regex NextDataRegex();
}
