using System.Net;
using HomeSeeker.Abstractions;
using HomeSeeker.Configuration;
using HomeSeeker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HomeSeeker.Scrapers;

/// <summary>
/// Shared search and detail-fetch flow for HTML portals: paging per property type, polite request
/// spacing, stopping the whole portal on 403 (bot detection) and partial-result warnings.
/// A new portal only implements URL building and page parsing.
/// </summary>
public abstract class ListingScraperBase : IListingScraper
{
    /// <summary>Named HttpClient with browser-like headers, registered by the host.</summary>
    public const string HttpClientName = "homeseeker-scraper";

    protected ListingScraperBase(
        IHttpClientFactory httpClientFactory,
        IOptions<HomeSeekerOptions> options,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        HttpClient = httpClientFactory.CreateClient(HttpClientName);
        Options = options.Value;
        Logger = logger;
    }

    public abstract string PortalName { get; }

    /// <summary>Portal domain (e.g. "olx.pl"); detail pages are fetched only from it and its subdomains.</summary>
    protected abstract string PortalDomain { get; }

    protected HttpClient HttpClient { get; }

    protected HomeSeekerOptions Options { get; }

    protected ILogger Logger { get; }

    /// <summary>Search results URL for one property type and 1-based page.</summary>
    protected abstract string BuildSearchUrl(SearchProfile profile, PropertyTypes type, int page);

    /// <summary>Listings found on a search results page; empty when there are no more results.</summary>
    protected abstract IReadOnlyList<ScrapedListing> ParseSearchPage(string html);

    /// <summary>Details text and coordinates from an ad page; null when nothing could be read.</summary>
    protected abstract ListingDetails? ParseDetailsPage(string html);

    public async Task<ScrapeResult> SearchAsync(SearchProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

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
                for (int page = 1; page <= Options.MaxSearchPagesPerPortal; page++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (requestsSent++ > 0)
                        await Task.Delay(Options.RequestDelay, cancellationToken).ConfigureAwait(false);

                    var url = BuildSearchUrl(profile, type, page);
                    Logger.LogDebug("Fetching {Portal} {Type} page {Page}: {Url}", PortalName, type, page, url);

                    try
                    {
                        var html = await HttpClient.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
                        pagesFetched++;

                        var listings = ParseSearchPage(html);
                        if (listings.Count == 0)
                        {
                            Logger.LogDebug("No more {Type} listings found on {Portal} page {Page}", type, PortalName, page);
                            break;
                        }

                        results.AddRange(listings.Select(l => l with { PropertyType = type }));
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
                    {
                        Logger.LogWarning("{Portal} returned 403 Forbidden - possible rate limiting or bot detection", PortalName);
                        failure = $"{PortalName} returned 403 Forbidden";
                        typeFailure = failure;
                        blocked = true;
                        break;
                    }
                    catch (HttpRequestException ex)
                    {
                        Logger.LogWarning(ex, "Failed to fetch {Portal} {Type} page {Page}", PortalName, type, page);
                        failure = $"{PortalName} request failed: {ex.Message}";
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
            Logger.LogDebug("{Portal} scraping cancelled", PortalName);
            failure = $"{PortalName} scraping cancelled";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error during {Portal} scraping", PortalName);
            failure = $"{PortalName} scraping error: {ex.Message}";
        }

        // Only treat the portal as failed when we never got a usable page.
        // A failure after at least one good page still yields a partial success.
        if (pagesFetched == 0 && failure is not null)
            return ScrapeResult.Failure(failure);

        Logger.LogInformation("{Portal} scraper found {Count} listings for profile {ProfileId}",
            PortalName, results.Count, profile.Id);

        var warnings = lostTypes.Select(t => $"{t.ToPolish()} - nie pobrano")
            .Concat(partialTypes.Select(p => $"{p.Type.ToPolish()} - niepełne wyniki ({p.Error})"))
            .ToList();
        return ScrapeResult.Success(results, warnings);
    }

    public async Task<ListingDetails?> GetDetailsAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!IsUrlOnDomain(url, PortalDomain))
            return null;

        try
        {
            var html = await HttpClient.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            return ParseDetailsPage(html);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to fetch {Portal} listing details {Url}", PortalName, url);
            return null;
        }
    }

    /// <summary>True for an absolute https URL on <paramref name="domain"/> or its subdomain.</summary>
    protected static bool IsUrlOnDomain(string url, string domain) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        (uri.Host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
         uri.Host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase));

    protected static ListingDetails? CreateDetails(string? text, GeoPoint? coordinates) =>
        text is null && coordinates is null ? null : new ListingDetails(text, coordinates);
}
