using System.Text.Json;
using HomeSeeker.Abstractions;
using HomeSeeker.Configuration;
using HomeSeeker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

    public MarketScanService(
        IEnumerable<IListingScraper> scrapers,
        IListingRepository repository,
        IListingEvaluator evaluator,
        IScanReportSender reportSender,
        IOptions<HomeSeekerOptions> options,
        ILogger<MarketScanService> logger)
    {
        _scrapers = scrapers;
        _repository = repository;
        _evaluator = evaluator;
        _reportSender = reportSender;
        _options = options.Value;
        _logger = logger;
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

        int totalFound = 0;
        int newCount = 0;
        int priceDrops = 0;
        int evaluated = 0;
        string? reportHtml = null;
        string? error = null;
        string? warnings = null;

        try
        {
            // Phase 1: Scrape listings from all portals
            var allScraped = new List<ScrapedListing>();
            var portalsAttempted = 0;
            var portalsSucceeded = 0;
            var portalErrors = new List<string>();

            foreach (var scraper in _scrapers)
            {
                portalsAttempted++;
                try
                {
                    _logger.LogDebug("Scraping {Portal} for profile {ProfileId}", scraper.PortalName, profileId);

                    var result = await scraper.SearchAsync(profile, cancellationToken).ConfigureAwait(false);
                    if (result.Succeeded)
                    {
                        portalsSucceeded++;
                        allScraped.AddRange(result.Listings);
                        foreach (var warning in result.Warnings)
                        {
                            portalErrors.Add($"{scraper.PortalName}: {warning}");
                            _logger.LogWarning("Scraper {Portal} partial result for profile {ProfileId}: {Warning}",
                                scraper.PortalName, profileId, warning);
                        }
                        _logger.LogDebug("{Portal} returned {Count} listings",
                            scraper.PortalName, result.Listings.Count);
                    }
                    else
                    {
                        portalErrors.Add($"{scraper.PortalName}: {result.Error ?? "unknown error"}");
                        _logger.LogWarning("Scraper {Portal} failed for profile {ProfileId}: {Error}",
                            scraper.PortalName, profileId, result.Error);
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
                        scraper.PortalName, profileId);
                }
            }

            // If every portal failed we must not record a "Completed" scan with zero
            // results; mark it failed so the lease is released without stamping
            // LastScannedAt and the profile is retried on the next cycle.
            if (portalsAttempted > 0 && portalsSucceeded == 0)
            {
                throw new InvalidOperationException(
                    "No portal completed successfully: " + string.Join("; ", portalErrors));
            }

            // Partial failures (a whole portal or one property type) don't fail the scan,
            // but are recorded on the run so missing data is visible in the scan history.
            if (portalErrors.Count > 0)
                warnings = Truncate("Niepełny skan: " + string.Join("; ", portalErrors), 2000);

            totalFound = allScraped.Count;
            _logger.LogInformation("Total scraped: {Count} listings from {Succeeded}/{Attempted} portals",
                totalFound, portalsSucceeded, portalsAttempted);

            // Phase 2: Upsert listings and track changes
            var upsertResults = new List<UpsertResult>();

            foreach (var scraped in allScraped)
            {
                try
                {
                    var result = await _repository.UpsertListingAsync(profileId, scraped, cancellationToken)
                        .ConfigureAwait(false);
                    upsertResults.Add(result);

                    if (result.IsNew) newCount++;
                    if (result.PriceDropped) priceDrops++;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to upsert listing {ExternalId} from {Portal}",
                        scraped.ExternalId, scraped.Portal);

                    // Drop any tracked-but-unsaved entity so later SaveChanges calls
                    // in this scope don't retry (and re-fail on) the same insert.
                    _repository.ResetTracking();
                }
            }

            // Phase 3: Select candidates for AI evaluation
            // Only new or price-dropped listings, sorted by price per sqm, limited by config
            var candidates = SelectCandidates(upsertResults, _options.MaxCandidatesPerScan);

            _logger.LogInformation("Selected {Count} candidates for AI evaluation", candidates.Count);

            // Details already included in search results (OLX) - no extra request needed.
            var scrapedDetails = allScraped
                .Where(s => !string.IsNullOrWhiteSpace(s.Details))
                .GroupBy(s => (s.Portal, s.ExternalId))
                .ToDictionary(g => g.Key, g => g.First().Details);

            // Phase 4: Evaluate candidates sequentially (to respect API limits)
            var detailRequests = 0;
            foreach (var candidate in candidates)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var listing = candidate.Listing;
                    if (!scrapedDetails.TryGetValue((listing.Portal, listing.ExternalId), out var details))
                    {
                        var scraper = _scrapers.FirstOrDefault(s =>
                            string.Equals(s.PortalName, listing.Portal, StringComparison.OrdinalIgnoreCase));
                        if (scraper is not null)
                        {
                            if (detailRequests++ > 0)
                                await Task.Delay(_options.RequestDelay, cancellationToken).ConfigureAwait(false);

                            details = await FetchDetailsAsync(scraper, listing, cancellationToken)
                                .ConfigureAwait(false);
                            if (details is null)
                                _logger.LogDebug("No details for listing {ListingId}, evaluating without description", listing.Id);
                        }
                    }

                    var evaluation = await _evaluator.EvaluateAsync(profile, listing, details, cancellationToken)
                        .ConfigureAwait(false);

                    if (evaluation is not null)
                    {
                        await _repository.SaveEvaluationAsync(candidate.Listing.Id, evaluation, cancellationToken)
                            .ConfigureAwait(false);
                        evaluated++;

                        _logger.LogDebug("Evaluated listing {ListingId}: score {Score}",
                            candidate.Listing.Id, evaluation.Score);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to evaluate listing {ListingId}", candidate.Listing.Id);
                    _repository.ResetTracking();
                }
            }

            // Phase 5: Generate report
            var topListings = await _repository.GetTopListingsAsync(
                profileId, _options.TopListingsInReport, cancellationToken).ConfigureAwait(false);

            // Update scan run with counts before generating report
            scanRun.ListingsFoundCount = totalFound;
            scanRun.NewListingsCount = newCount;
            scanRun.PriceDropsCount = priceDrops;
            scanRun.EvaluatedCount = evaluated;

            reportHtml = await _evaluator.WriteReportHtmlAsync(profile, topListings, scanRun, cancellationToken)
                .ConfigureAwait(false);

            // Phase 6: Send email - only when there is something new to tell the
            // household about; the full report is always available via the API.
            if (newCount == 0 && priceDrops == 0)
            {
                _logger.LogInformation("Scan {ScanRunId}: no new listings or price drops, skipping email", scanRun.Id);
            }
            else if (!string.IsNullOrWhiteSpace(profile.NotificationEmail))
            {
                var subject = $"HomeSeeker: {profile.City} - {newCount} nowych, {priceDrops} obniżek";

                var sent = await _reportSender.SendAsync(
                    profile.NotificationEmail, subject, reportHtml, cancellationToken).ConfigureAwait(false);

                if (sent)
                {
                    _logger.LogInformation("Report sent to {Email}", profile.NotificationEmail);
                }
                else
                {
                    _logger.LogWarning("Failed to send report to {Email}", profile.NotificationEmail);
                }
            }

            _logger.LogInformation(
                "Scan {ScanRunId} completed: found={Found}, new={New}, drops={Drops}, evaluated={Evaluated}",
                scanRun.Id, totalFound, newCount, priceDrops, evaluated);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Scan {ScanRunId} was cancelled", scanRun.Id);
            error = "Scan cancelled";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan {ScanRunId} failed", scanRun.Id);
            error = Truncate(ex.Message, 2000);
        }

        // Complete the scan run.
        // IMPORTANT: never pass the original (possibly cancelled) token here, or the
        // final status write will throw and the run will stay in `Running` forever (H1).
        // Use a short bounded timeout off a fresh source so a hang can't block shutdown.
        var status = error is null ? ScanStatus.Completed : ScanStatus.Failed;
        var persisted = false;

        try
        {
            using var finalWriteCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await _repository.CompleteScanRunAsync(
                scanRun.Id, status, totalFound, newCount, priceDrops, evaluated, reportHtml, error ?? warnings,
                finalWriteCts.Token)
                .ConfigureAwait(false);
            persisted = true;
        }
        catch (Exception finalWriteEx)
        {
            // Last-resort log: we tried, but the DB is unreachable. A startup
            // reconciliation job (recommended as a follow-up) should mark stale
            // `Running` rows as `Failed`.
            _logger.LogError(finalWriteEx,
                "Failed to persist final status for scan {ScanRunId}; run may stay in Running state",
                scanRun.Id);
        }

        // Report success only when the run both completed and its final state was
        // recorded; otherwise the caller must not stamp LastScannedAt.
        return status == ScanStatus.Completed && persisted;
    }

    private async Task<string?> FetchDetailsAsync(
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

    /// <summary>
    /// Picks new/price-dropped listings, cheapest per m² first, sharing the cap fairly
    /// between property types (round-robin) so cheaper-per-m² houses can't crowd out
    /// apartments. Slots a type can't use go to the other type.
    /// </summary>
    internal static List<UpsertResult> SelectCandidates(IEnumerable<UpsertResult> upsertResults, int max)
    {
        var queues = upsertResults
            .Where(r => r.IsNew || r.PriceDropped)
            .GroupBy(r => r.Listing.PropertyType)
            .OrderBy(g => g.Key)
            .Select(g => new Queue<UpsertResult>(g.OrderBy(r => PricePerSqm(r.Listing))))
            .ToList();

        var selected = new List<UpsertResult>(max);
        while (selected.Count < max && queues.Any(q => q.Count > 0))
        {
            foreach (var queue in queues)
            {
                if (selected.Count >= max) break;
                if (queue.Count > 0) selected.Add(queue.Dequeue());
            }
        }

        return selected;
    }

    private static decimal PricePerSqm(HouseListing listing) =>
        listing.AreaSqm > 0 ? listing.Price / listing.AreaSqm : decimal.MaxValue;

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
