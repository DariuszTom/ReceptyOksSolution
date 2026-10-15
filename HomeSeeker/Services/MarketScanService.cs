using System.Text.Json;
using HomeSeeker.Abstractions;
using HomeSeeker.Configuration;
using HomeSeeker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReceptyOks.Shared.Misc;

namespace HomeSeeker.Services;

/// <summary>
/// Orchestrates the market scanning process.
/// </summary>
public sealed class MarketScanService : IMarketScanService
{
    private readonly IEnumerable<IListingScraper> _scrapers;
    private readonly IListingRepository _repository;
    private readonly IListingEvaluator _evaluator;
    private readonly IScanReportSender _reportSender;
    private readonly HomeSeekerOptions _options;
    private readonly ILogger<MarketScanService> _logger;
    private readonly ICityCenterProvider? _cityCenterProvider;
    private readonly IPropertyValuationProvider? _valuationProvider;

    public MarketScanService(
        IEnumerable<IListingScraper> scrapers,
        IListingRepository repository,
        IListingEvaluator evaluator,
        IScanReportSender reportSender,
        IOptions<HomeSeekerOptions> options,
        ILogger<MarketScanService> logger,
        ICityCenterProvider? cityCenterProvider = null,
        IPropertyValuationProvider? valuationProvider = null)
    {
        _scrapers = scrapers;
        _repository = repository;
        _evaluator = evaluator;
        _reportSender = reportSender;
        _options = options.Value;
        _logger = logger;
        _cityCenterProvider = cityCenterProvider;
        _valuationProvider = valuationProvider;
    }

    public async Task<bool> RunScanAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var profile = await _repository.GetProfileByIdAsync(profileId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            _logger.LogWarning("Profile {ProfileId} not found", profileId);
            return false;
        }

        if (profile.IsDeleted || !profile.IsActive)
        {
            _logger.LogDebug("Profile {ProfileId} is deleted or inactive, skipping scan", profileId);
            return false;
        }

        var scanRun = await _repository.CreateScanRunAsync(profileId, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Starting scan {ScanRunId} for profile {ProfileId} in {City}",
            scanRun.Id, profileId, profile.City);

        // Counters are filled in as phases complete, so a failed or cancelled run still
        // records how far it got.
        var stats = new ScanStats();
        string? reportHtml = null;
        string? error = null;

        try
        {
            var scraped = await ScrapePortalsAsync(profile, stats, cancellationToken).ConfigureAwait(false);
            await ExcludeOutOfRangeAsync(profile, scraped, cancellationToken).ConfigureAwait(false);

            stats.Found = scraped.Count;
            _logger.LogInformation("Total scraped: {Count} listings", stats.Found);

            var upsertResults = await UpsertListingsAsync(profileId, scraped, stats, cancellationToken)
                .ConfigureAwait(false);

            // Only new or price-dropped listings (plus unevaluated backlog), ranked by price per m²
            // relative to the local (district) median, limited by config.
            var priceStats = new MarketPriceStats(
                await GetMarketSampleAsync(profileId, upsertResults, cancellationToken).ConfigureAwait(false));
            var candidates = ListingSelection.SelectCandidates(upsertResults, _options.MaxCandidatesPerScan, priceStats);
            _logger.LogInformation("Selected {Count} candidates for AI evaluation", candidates.Count);

            var cityCenter = upsertResults.Count > 0
                ? await GetCityCenterAsync(profile.City, cancellationToken).ConfigureAwait(false)
                : null;

            await UpdateDistancesAsync(upsertResults, candidates, scraped, cityCenter, cancellationToken)
                .ConfigureAwait(false);

            await EvaluateCandidatesAsync(profile, candidates, scraped, priceStats, cityCenter, stats, cancellationToken)
                .ConfigureAwait(false);

            reportHtml = await WriteReportAsync(profile, scanRun, stats, cancellationToken).ConfigureAwait(false);
            await SendReportAsync(profile, scanRun, stats, reportHtml, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Scan {ScanRunId} completed: found={Found}, new={New}, drops={Drops}, evaluated={Evaluated}",
                scanRun.Id, stats.Found, stats.New, stats.PriceDrops, stats.Evaluated);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Scan {ScanRunId} was cancelled", scanRun.Id);
            error = "Scan cancelled";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan {ScanRunId} failed", scanRun.Id);
            error = ex.Message.Truncate(2000);
        }

        return await CompleteScanRunAsync(scanRun, stats, reportHtml, error).ConfigureAwait(false);
    }

    /// <summary>
    /// Phase 1: scrapes every portal. Partial failures (a whole portal or one property type)
    /// are recorded in <see cref="ScanStats.Warnings"/>; throws when no portal succeeded, so a
    /// zero-result run is never recorded as completed and the profile is retried next cycle.
    /// </summary>
    private async Task<List<ScrapedListing>> ScrapePortalsAsync(
        SearchProfile profile, ScanStats stats, CancellationToken cancellationToken)
    {
        var allScraped = new List<ScrapedListing>();
        var portalsAttempted = 0;
        var portalsSucceeded = 0;
        var portalErrors = new List<string>();

        foreach (var scraper in _scrapers)
        {
            portalsAttempted++;
            try
            {
                _logger.LogDebug("Scraping {Portal} for profile {ProfileId}", scraper.PortalName, profile.Id);

                var result = await scraper.SearchAsync(profile, cancellationToken).ConfigureAwait(false);
                if (result.Succeeded)
                {
                    portalsSucceeded++;
                    allScraped.AddRange(result.Listings);
                    foreach (var warning in result.Warnings)
                    {
                        portalErrors.Add($"{scraper.PortalName}: {warning}");
                        _logger.LogWarning("Scraper {Portal} partial result for profile {ProfileId}: {Warning}",
                            scraper.PortalName, profile.Id, warning);
                    }
                    _logger.LogDebug("{Portal} returned {Count} listings",
                        scraper.PortalName, result.Listings.Count);
                }
                else
                {
                    portalErrors.Add($"{scraper.PortalName}: {result.Error ?? "unknown error"}");
                    _logger.LogWarning("Scraper {Portal} failed for profile {ProfileId}: {Error}",
                        scraper.PortalName, profile.Id, result.Error);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                portalErrors.Add($"{scraper.PortalName}: {ex.Message}");
                _logger.LogWarning(ex, "Scraper {Portal} threw for profile {ProfileId}",
                    scraper.PortalName, profile.Id);
            }
        }

        if (portalsAttempted > 0 && portalsSucceeded == 0)
        {
            throw new InvalidOperationException(
                "No portal completed successfully: " + string.Join("; ", portalErrors));
        }

        if (portalErrors.Count > 0)
            stats.Warnings = ("Niepełny skan: " + string.Join("; ", portalErrors)).Truncate(2000);

        _logger.LogDebug("Scraped {Count} listings from {Succeeded}/{Attempted} portals",
            allScraped.Count, portalsSucceeded, portalsAttempted);
        return allScraped;
    }

    /// <summary>
    /// Portals don't always honour the price/area filters in the URL (Otodom mixes in
    /// "similar" offers), so the profile's ranges are enforced here before anything is stored.
    /// Removes out-of-range listings from <paramref name="scraped"/>; already-stored ones are
    /// refreshed and hidden so lists and reports never keep showing them at a stale price.
    /// </summary>
    private async Task ExcludeOutOfRangeAsync(
        SearchProfile profile, List<ScrapedListing> scraped, CancellationToken cancellationToken)
    {
        var outOfRange = scraped.Where(s => !ListingSelection.MatchesCriteria(profile, s)).ToList();
        if (outOfRange.Count == 0)
            return;

        scraped.RemoveAll(s => !ListingSelection.MatchesCriteria(profile, s));
        _logger.LogInformation("Dropped {Count} listings outside the profile's price/area/rooms range",
            outOfRange.Count);

        foreach (var listing in outOfRange)
        {
            try
            {
                await _repository.ExcludeListingAsync(profile.Id, listing, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to exclude listing {ExternalId} from {Portal}",
                    listing.ExternalId, listing.Portal);
            }
        }
    }

    /// <summary>
    /// Phase 2: stores the scraped listings, tracking new ones and price drops.
    /// A failing listing is skipped without aborting the scan.
    /// </summary>
    private async Task<List<UpsertResult>> UpsertListingsAsync(
        Guid profileId, List<ScrapedListing> scraped, ScanStats stats, CancellationToken cancellationToken)
    {
        var upsertResults = new List<UpsertResult>(scraped.Count);

        foreach (var listing in scraped)
        {
            try
            {
                var result = await _repository.UpsertListingAsync(profileId, listing, cancellationToken)
                    .ConfigureAwait(false);
                upsertResults.Add(result);

                if (result.IsNew) stats.New++;
                if (result.PriceDropped) stats.PriceDrops++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upsert listing {ExternalId} from {Portal}",
                    listing.ExternalId, listing.Portal);

                // Drop any tracked-but-unsaved entity so later SaveChanges calls
                // in this scope don't retry (and re-fail on) the same insert.
                _repository.ResetTracking();
            }
        }

        return upsertResults;
    }

    /// <summary>
    /// Phase 3: evaluates candidates sequentially (to respect API limits). Descriptions come
    /// from the search results when present (OLX), otherwise from the portal's detail page.
    /// </summary>
    private async Task EvaluateCandidatesAsync(
        SearchProfile profile,
        IReadOnlyList<UpsertResult> candidates,
        IReadOnlyList<ScrapedListing> scraped,
        MarketPriceStats priceStats,
        GeoPoint? cityCenter,
        ScanStats stats,
        CancellationToken cancellationToken)
    {
        var scrapedByKey = ToScrapedLookup(scraped);

        var detailRequests = 0;
        foreach (var candidate in candidates)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var listing = candidate.Listing;
            try
            {
                scrapedByKey.TryGetValue((listing.Portal, listing.ExternalId), out var scrapedListing);
                var details = scrapedListing?.Details;
                var coordinates = scrapedListing?.Coordinates;
                if (string.IsNullOrWhiteSpace(details))
                {
                    var scraper = _scrapers.FirstOrDefault(s =>
                        string.Equals(s.PortalName, listing.Portal, StringComparison.OrdinalIgnoreCase));
                    if (scraper is not null)
                    {
                        if (detailRequests++ > 0)
                            await Task.Delay(_options.RequestDelay, cancellationToken).ConfigureAwait(false);

                        var fetched = await FetchDetailsAsync(scraper, listing, cancellationToken).ConfigureAwait(false);
                        details = fetched?.Text;
                        // The ad page is more precise than the search results.
                        coordinates = fetched?.Coordinates ?? coordinates;
                        if (details is null)
                            _logger.LogDebug("No details for listing {ListingId}, evaluating without description", listing.Id);
                    }
                }

                var benchmark = await EnrichBenchmarkAsync(
                        priceStats.GetBenchmark(listing), listing, coordinates, cityCenter, cancellationToken)
                    .ConfigureAwait(false);

                if (benchmark?.DistanceFromCenterKm is { } distanceKm)
                    await SaveDistanceAsync(listing, distanceKm, benchmark.DistanceIsApproximate, cancellationToken)
                        .ConfigureAwait(false);

                var evaluation = await _evaluator.EvaluateAsync(
                        profile, listing, details, benchmark, cancellationToken)
                    .ConfigureAwait(false);
                if (evaluation is null)
                    continue;

                await _repository.SaveEvaluationAsync(listing.Id, evaluation, cancellationToken).ConfigureAwait(false);
                stats.Evaluated++;
                _logger.LogDebug("Evaluated listing {ListingId}: score {Score}", listing.Id, evaluation.Score);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to evaluate listing {ListingId}", listing.Id);
                _repository.ResetTracking();
            }
        }
    }

    /// <summary>
    /// Phase 4: renders the HTML report of the profile's top listings.
    /// </summary>
    private async Task<string> WriteReportAsync(
        SearchProfile profile, ScanRun scanRun, ScanStats stats, CancellationToken cancellationToken)
    {
        var topListings = await _repository.GetTopListingsAsync(
            profile.Id, _options.TopListingsInReport, cancellationToken).ConfigureAwait(false);

        // The report shows this run's counts.
        scanRun.ListingsFoundCount = stats.Found;
        scanRun.NewListingsCount = stats.New;
        scanRun.PriceDropsCount = stats.PriceDrops;
        scanRun.EvaluatedCount = stats.Evaluated;

        return await _evaluator.WriteReportHtmlAsync(profile, topListings, scanRun, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Phase 5: e-mails the report - only when there is something new to tell the
    /// household about; the full report is always available via the API.
    /// </summary>
    private async Task SendReportAsync(
        SearchProfile profile, ScanRun scanRun, ScanStats stats, string reportHtml, CancellationToken cancellationToken)
    {
        if (stats.New == 0 && stats.PriceDrops == 0)
        {
            _logger.LogInformation("Scan {ScanRunId}: no new listings or price drops, skipping email", scanRun.Id);
            return;
        }

        if (string.IsNullOrWhiteSpace(profile.NotificationEmail))
            return;

        var subject = $"HomeSeeker: {profile.City} - {stats.New} nowych, {stats.PriceDrops} obniżek";
        var sent = await _reportSender.SendAsync(profile.NotificationEmail, subject, reportHtml, cancellationToken)
            .ConfigureAwait(false);

        if (sent)
            _logger.LogInformation("Report sent to {Email}", profile.NotificationEmail);
        else
            _logger.LogWarning("Failed to send report to {Email}", profile.NotificationEmail);
    }

    /// <summary>
    /// Persists the run's final state. Returns true only when the run both completed and its
    /// final state was recorded; otherwise the caller must not stamp LastScannedAt.
    /// </summary>
    private async Task<bool> CompleteScanRunAsync(ScanRun scanRun, ScanStats stats, string? reportHtml, string? error)
    {
        var status = error is null ? ScanStatus.Completed : ScanStatus.Failed;

        try
        {
            // IMPORTANT: never pass the original (possibly cancelled) token here, or the
            // final status write will throw and the run will stay in `Running` forever (H1).
            // Use a short bounded timeout off a fresh source so a hang can't block shutdown.
            using var finalWriteCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await _repository.CompleteScanRunAsync(
                    scanRun.Id, status, stats.Found, stats.New, stats.PriceDrops, stats.Evaluated,
                    reportHtml, error ?? stats.Warnings, finalWriteCts.Token)
                .ConfigureAwait(false);
            return status == ScanStatus.Completed;
        }
        catch (Exception finalWriteEx)
        {
            // Last-resort log: we tried, but the DB is unreachable. A startup
            // reconciliation job (recommended as a follow-up) should mark stale
            // `Running` rows as `Failed`.
            _logger.LogError(finalWriteEx,
                "Failed to persist final status for scan {ScanRunId}; run may stay in Running state",
                scanRun.Id);
            return false;
        }
    }

    /// <summary>
    /// Mutable per-run counters shared by the scan phases.
    /// </summary>
    private sealed class ScanStats
    {
        public int Found { get; set; }
        public int New { get; set; }
        public int PriceDrops { get; set; }
        public int Evaluated { get; set; }
        public string? Warnings { get; set; }
    }

    /// <summary>
    /// Market sample for price medians: every listing seen within <see cref="HomeSeekerOptions.ListingRetention"/>
    /// (so the sample grows across scans) in the cities returned by this scan, merged with
    /// this scan's listings. Falls back to
    /// this scan alone when the history can't be loaded.
    /// </summary>
    private async Task<IReadOnlyCollection<HouseListing>> GetMarketSampleAsync(
        Guid profileId, List<UpsertResult> upsertResults, CancellationToken cancellationToken)
    {
        // Current-scan entries win over history so the latest prices are used.
        var sample = new Dictionary<Guid, HouseListing>();
        foreach (var result in upsertResults)
            sample.TryAdd(result.Listing.Id, result.Listing);

        try
        {
            var history = await _repository
                .GetMarketSampleAsync(profileId, DateTime.UtcNow - _options.ListingRetention, cancellationToken)
                .ConfigureAwait(false);

            // History may hold listings from a city the profile no longer targets (the city
            // can be edited); districts are keyed without the city, so only keep listings
            // from the cities this scan actually returned.
            var cities = upsertResults
                .Select(r => MarketPriceStats.CityOf(r.Listing.Location))
                .ToHashSet();

            foreach (var listing in history ?? [])
            {
                if (cities.Contains(MarketPriceStats.CityOf(listing.Location)))
                    sample.TryAdd(listing.Id, listing);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Loading market sample failed for profile {ProfileId}; using current scan only", profileId);
            _repository.ResetTracking();
        }

        _logger.LogDebug("Market sample for price medians: {Count} listings ({Current} from this scan)",
            sample.Count, upsertResults.Count);
        return sample.Values;
    }

    private async Task<GeoPoint?> GetCityCenterAsync(string city, CancellationToken cancellationToken)
    {
        if (_cityCenterProvider is null || string.IsNullOrWhiteSpace(city))
            return null;

        try
        {
            return await _cityCenterProvider.GetCenterAsync(city, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Resolving city centre failed for {City}", city);
            return null;
        }
    }

    /// <summary>
    /// Adds location context the portals' own prices can't provide: distance from the centre
    /// and, for apartments, transaction prices (RCN) around the listing. Without these, a cheap
    /// listing on the outskirts looks like a bargain.
    /// </summary>
    private async Task<PriceBenchmark?> EnrichBenchmarkAsync(
        PriceBenchmark? benchmark,
        HouseListing listing,
        GeoPoint? coordinates,
        GeoPoint? cityCenter,
        CancellationToken cancellationToken)
    {
        if (coordinates is not { } point)
            return benchmark;

        double? distance = cityCenter is { } center ? Math.Round(point.DistanceKmTo(center), 1) : null;

        RcnValuation? rcn = null;
        if (_valuationProvider is not null && listing.PropertyType == PropertyTypes.Apartment && listing.AreaSqm > 0)
        {
            try
            {
                rcn = await _valuationProvider.GetApartmentValuationAsync(point, listing.AreaSqm, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RCN valuation failed for listing {ListingId}", listing.Id);
            }
        }

        if (distance is null && rcn is null)
            return benchmark;

        return (benchmark ?? new PriceBenchmark(null, 0, 0, 0)) with
        {
            DistanceFromCenterKm = distance,
            DistanceIsApproximate = distance is not null && point.IsApproximate,
            Rcn = rcn
        };
    }

    private static Dictionary<(string Portal, string ExternalId), ScrapedListing> ToScrapedLookup(
        IReadOnlyList<ScrapedListing> scraped) =>
        scraped
            .GroupBy(s => (s.Portal, s.ExternalId))
            .ToDictionary(g => g.Key, g => g.First());

    /// <summary>
    /// Stores the distance for every scraped listing whose search result has coordinates, not only
    /// for AI candidates, so the distance filter covers the whole list. Candidates are skipped:
    /// they get a more precise distance from the ad page during evaluation.
    /// </summary>
    private async Task UpdateDistancesAsync(
        IReadOnlyList<UpsertResult> upsertResults,
        IReadOnlyList<UpsertResult> candidates,
        IReadOnlyList<ScrapedListing> scraped,
        GeoPoint? cityCenter,
        CancellationToken cancellationToken)
    {
        if (cityCenter is not { } center)
            return;

        var scrapedByKey = ToScrapedLookup(scraped);
        var candidateIds = candidates.Select(c => c.Listing.Id).ToHashSet();

        foreach (var result in upsertResults)
        {
            var listing = result.Listing;
            if (candidateIds.Contains(listing.Id) ||
                !scrapedByKey.TryGetValue((listing.Portal, listing.ExternalId), out var scrapedListing) ||
                scrapedListing.Coordinates is not { } point)
            {
                continue;
            }

            var distanceKm = Math.Round(point.DistanceKmTo(center), 1);
            if (listing.DistanceFromCenterKm == distanceKm && listing.DistanceIsApproximate == point.IsApproximate)
                continue;

            await SaveDistanceAsync(listing, distanceKm, point.IsApproximate, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Persists the distance so the app can show and filter by it; a failure only loses the distance.
    /// </summary>
    private async Task SaveDistanceAsync(
        HouseListing listing, double distanceKm, bool isApproximate, CancellationToken cancellationToken)
    {
        try
        {
            await _repository.SaveDistanceAsync(listing.Id, distanceKm, isApproximate, cancellationToken)
                .ConfigureAwait(false);
            listing.DistanceFromCenterKm = distanceKm;
            listing.DistanceIsApproximate = isApproximate;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Saving distance failed for listing {ListingId}", listing.Id);
        }
    }

    private async Task<ListingDetails?> FetchDetailsAsync(
        IListingScraper scraper, HouseListing listing, CancellationToken cancellationToken)
    {
        try
        {
            return await scraper.GetDetailsAsync(listing.Url, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fetching details failed for listing {ListingId}", listing.Id);
            return null;
        }
    }

}
