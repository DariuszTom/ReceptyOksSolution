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
                _logger.LogDebug("Fetching Otodom page {Page}: {Url}", page, url);

                try
                {
                    var html = await _httpClient.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
                    var listings = ParseNextData(html);

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
                    _logger.LogWarning("Otodom returned 403 Forbidden - possible rate limiting or bot detection");
                    break;
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch Otodom page {Page}", page);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Otodom scraping cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Otodom scraping");
        }

        _logger.LogInformation("Otodom scraper found {Count} listings for profile {ProfileId}",
            results.Count, profile.Id);

        return results;
    }

    private static string BuildSearchUrl(SearchProfile profile, int page)
    {
        var city = NormalizeCity(profile.City);
        var url = $"https://www.otodom.pl/pl/wyniki/sprzedaz/dom/{city}?page={page}";

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
                locationProp.TryGetProperty("address", out var addressProp) &&
                addressProp.TryGetProperty("city", out var cityProp) &&
                cityProp.TryGetProperty("name", out var cityName))
            {
                location = cityName.GetString();

                if (addressProp.TryGetProperty("district", out var districtProp) &&
                    districtProp.TryGetProperty("name", out var districtName))
                {
                    location = $"{location}, {districtName.GetString()}";
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

    [GeneratedRegex(@"<script\s+id=""__NEXT_DATA__""\s+type=""application/json""[^>]*>(.*?)</script>", RegexOptions.Singleline)]
    private static partial Regex NextDataRegex();
}
