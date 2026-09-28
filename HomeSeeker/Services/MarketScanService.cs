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

    public async Task RunScanAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var profile = await _repository.GetProfileByIdAsync(profileId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            _logger.LogWarning("Profile {ProfileId} not found", profileId);
            return;
        }

        if (profile.IsDeleted || !profile.IsActive)
        {
            _logger.LogDebug("Profile {ProfileId} is deleted or inactive, skipping scan", profileId);
            return;
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

        try
        {
            // Phase 1: Scrape listings from all portals
            var allScraped = new List<ScrapedListing>();

            foreach (var scraper in _scrapers)
            {
                try
                {
                    _logger.LogDebug("Scraping {Portal} for profile {ProfileId}", scraper.PortalName, profileId);

                    var listings = await scraper.SearchAsync(profile, cancellationToken).ConfigureAwait(false);
                    allScraped.AddRange(listings);

                    _logger.LogDebug("{Portal} returned {Count} listings", scraper.PortalName, listings.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Scraper {Portal} failed for profile {ProfileId}",
                        scraper.PortalName, profileId);
                }
            }

            totalFound = allScraped.Count;
            _logger.LogInformation("Total scraped: {Count} listings from {ScraperCount} portals",
                totalFound, _scrapers.Count());

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
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to upsert listing {ExternalId} from {Portal}",
                        scraped.ExternalId, scraped.Portal);
                }
            }

            // Phase 3: Select candidates for AI evaluation
            // Only new or price-dropped listings, sorted by price per sqm, limited by config
            var candidates = upsertResults
                .Where(r => r.IsNew || r.PriceDropped)
                .OrderBy(r => r.Listing.Price / r.Listing.AreaSqm)
                .Take(_options.MaxCandidatesPerScan)
                .ToList();

            _logger.LogInformation("Selected {Count} candidates for AI evaluation", candidates.Count);

            // Phase 4: Evaluate candidates sequentially (to respect API limits)
            foreach (var candidate in candidates)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var evaluation = await _evaluator.EvaluateAsync(profile, candidate.Listing, cancellationToken)
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
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to evaluate listing {ListingId}", candidate.Listing.Id);
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

            // Phase 6: Send email
            if (!string.IsNullOrWhiteSpace(profile.NotificationEmail))
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
            error = ex.Message;
        }

        // Complete the scan run
        var status = error is null ? ScanStatus.Completed : ScanStatus.Failed;

        await _repository.CompleteScanRunAsync(
            scanRun.Id, status, totalFound, newCount, priceDrops, evaluated, reportHtml, error, cancellationToken)
            .ConfigureAwait(false);
    }
}
