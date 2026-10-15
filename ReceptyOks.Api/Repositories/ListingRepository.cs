using System.Text.Json;
using HomeSeeker.Abstractions;
using HomeSeeker.Configuration;
using HomeSeeker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReceptyOks.Api.DbUtility;

namespace ReceptyOks.Api.Repositories;

/// <summary>
/// Repository implementation for HomeSeeker data operations.
/// </summary>
public sealed class ListingRepository : IListingRepository
{
    private readonly HomeSeekerDbContext _db;
    private readonly HomeSeekerOptions _options;

    public ListingRepository(HomeSeekerDbContext db, IOptions<HomeSeekerOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    // Maximum time a single scan is expected to run. Lease is held for this long;
    // afterwards it's considered expired and may be re-acquired by another instance.
    private static readonly TimeSpan ScanLeaseDuration = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Fixed primary key of the single household profile. Using a constant key makes
    /// "create if missing" atomic: two concurrent first writers both try to insert the
    /// same row and the database's primary-key constraint rejects the loser.
    /// </summary>
    public static readonly Guid SharedProfileId = new("6d2a1f0e-9c3b-4d7a-8e5f-1a2b3c4d5e6f");

    public async Task<SearchProfile?> GetSharedProfileAsync(CancellationToken cancellationToken = default)
    {
        // Exactly one non-deleted profile is expected. If legacy data left several,
        // the most recently updated one wins so edits are never silently lost.
        return await _db.SearchProfiles
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.UpdatedAt)
            .ThenBy(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<SearchProfile> UpsertSharedProfileAsync(
        SearchProfileRequest request,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var profile = await _db.SearchProfiles
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.UpdatedAt)
            .ThenBy(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (profile is null)
        {
            profile = new SearchProfile
            {
                Id = SharedProfileId,
                CreatedAt = now
            };
            _db.SearchProfiles.Add(profile);

            ApplyRequest(profile, request, updatedBy, now);

            try
            {
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return profile;
            }
            catch (DbUpdateException)
            {
                // Lost the race: another request created the shared profile first.
                // Fall through and update that row instead.
                _db.ChangeTracker.Clear();
                profile = await _db.SearchProfiles
                    .FirstAsync(p => p.Id == SharedProfileId, cancellationToken)
                    .ConfigureAwait(false);
                profile.IsDeleted = false;
            }
        }

        ApplyRequest(profile, request, updatedBy, now);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return profile;
    }

    private static void ApplyRequest(SearchProfile profile, SearchProfileRequest request, string updatedBy, DateTime now)
    {
        profile.OwnerId = updatedBy;
        profile.City = request.City;
        profile.District = request.District;
        profile.MinPrice = request.MinPrice;
        profile.MaxPrice = request.MaxPrice;
        profile.MinAreaSqm = request.MinAreaSqm;
        profile.MaxAreaSqm = request.MaxAreaSqm;
        profile.ExtraCriteria = request.ExtraCriteria;
        profile.NotificationEmail = NotificationEmails.Normalize(request.NotificationEmail);
        profile.IsActive = request.IsActive;
        profile.UpdatedAt = now;
    }

    public async Task<SearchProfile?> GetProfileByIdAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        return await _db.SearchProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == profileId && !p.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Guid?> TryAcquireScanLeaseAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        // Periodic lease: requires profile to be due AND lease free.
        // Lease is set so concurrent instances won't double-scan; LastScannedAt
        // is updated only on successful completion (see ReleaseScanLeaseAsync).
        var now = DateTime.UtcNow;
        var cutoff = now - _options.ScanInterval;
        var leaseUntil = now + ScanLeaseDuration;
        var token = Guid.NewGuid();

        var updated = await _db.SearchProfiles
            .Where(p => p.Id == profileId && !p.IsDeleted && p.IsActive)
            .Where(p => p.LastScannedAt == null || p.LastScannedAt < cutoff)
            .Where(p => p.ScanLeaseUntil == null || p.ScanLeaseUntil < now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.ScanLeaseUntil, leaseUntil)
                .SetProperty(p => p.ScanLeaseToken, token)
                .SetProperty(p => p.UpdatedAt, now),
                cancellationToken)
            .ConfigureAwait(false);

        return updated > 0 ? token : null;
    }

    public async Task<bool> ReleaseScanLeaseAsync(Guid profileId, Guid leaseToken, bool markScanned, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Only the current holder may release. If our lease expired and another
        // instance took over, the token no longer matches and this is a no-op.
        var query = _db.SearchProfiles.Where(p => p.Id == profileId && p.ScanLeaseToken == leaseToken);

        int updated;
        if (markScanned)
        {
            updated = await query
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => (DateTime?)p.LastScannedAt, now)
                    .SetProperty(p => (DateTime?)p.ScanLeaseUntil, (DateTime?)null)
                    .SetProperty(p => (Guid?)p.ScanLeaseToken, (Guid?)null)
                    .SetProperty(p => p.UpdatedAt, now),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            // Failed or cancelled scan: free the lease but leave LastScannedAt untouched
            // so the profile is retried on the next cycle instead of after a full interval.
            updated = await query
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => (DateTime?)p.ScanLeaseUntil, (DateTime?)null)
                    .SetProperty(p => (Guid?)p.ScanLeaseToken, (Guid?)null)
                    .SetProperty(p => p.UpdatedAt, now),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return updated > 0;
    }

    public async Task<ScanRun> CreateScanRunAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var scanRun = new ScanRun
        {
            Id = Guid.NewGuid(),
            SearchProfileId = profileId,
            Status = ScanStatus.Running,
            StartedAt = DateTime.UtcNow
        };

        _db.ScanRuns.Add(scanRun);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return scanRun;
    }

    public async Task CompleteScanRunAsync(
        Guid scanRunId,
        ScanStatus status,
        int listingsFoundCount,
        int newListingsCount,
        int priceDropsCount,
        int evaluatedCount,
        string? reportHtml,
        string? error,
        CancellationToken cancellationToken = default)
    {
        await _db.ScanRuns
            .Where(s => s.Id == scanRunId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, status)
                .SetProperty(r => r.CompletedAt, DateTime.UtcNow)
                .SetProperty(r => r.ListingsFoundCount, listingsFoundCount)
                .SetProperty(r => r.NewListingsCount, newListingsCount)
                .SetProperty(r => r.PriceDropsCount, priceDropsCount)
                .SetProperty(r => r.EvaluatedCount, evaluatedCount)
                .SetProperty(r => r.ReportHtml, reportHtml)
                .SetProperty(r => r.Error, error),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<UpsertResult> UpsertListingAsync(
        Guid profileId,
        ScrapedListing scraped,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Check if listing already exists
        var existing = await _db.HouseListings
            .FirstOrDefaultAsync(l =>
                l.SearchProfileId == profileId &&
                l.Portal == scraped.Portal &&
                l.ExternalId == scraped.ExternalId,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            // Update existing listing
            var priceDropped = scraped.Price < existing.Price;

            if (priceDropped)
            {
                existing.PreviousPrice = existing.Price;
            }

            existing.Price = scraped.Price;
            existing.AreaSqm = scraped.AreaSqm;
            existing.Title = scraped.Title;
            existing.Url = scraped.Url;
            existing.Location = scraped.Location;
            existing.LastSeenAt = now;
            existing.UpdatedAt = now;

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return new UpsertResult
            {
                Listing = existing,
                IsNew = false,
                PriceDropped = priceDropped
            };
        }

        // Create new listing
        var newListing = new HouseListing
        {
            Id = Guid.NewGuid(),
            SearchProfileId = profileId,
            Portal = scraped.Portal,
            ExternalId = scraped.ExternalId,
            Url = scraped.Url,
            Title = scraped.Title,
            Price = scraped.Price,
            AreaSqm = scraped.AreaSqm,
            Location = scraped.Location,
            FirstSeenAt = now,
            LastSeenAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.HouseListings.Add(newListing);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new UpsertResult
        {
            Listing = newListing,
            IsNew = true,
            PriceDropped = false
        };
    }

    public void ResetTracking() => _db.ChangeTracker.Clear();

    public async Task SaveEvaluationAsync(
        Guid listingId,
        ListingEvaluation evaluation,
        CancellationToken cancellationToken = default)
    {
        // Model output is unbounded; clamp to the column sizes declared in
        // HomeSeekerDbContext so a verbose answer cannot fail the whole evaluation.
        var prosJson = SerializeBounded(evaluation.Pros, HomeSeekerDbContext.AiListJsonMaxLength);
        var consJson = SerializeBounded(evaluation.Cons, HomeSeekerDbContext.AiListJsonMaxLength);
        var summary = Truncate(evaluation.Summary, HomeSeekerDbContext.AiSummaryMaxLength);
        var priceAssessment = Truncate(evaluation.PriceAssessment, HomeSeekerDbContext.AiPriceAssessmentMaxLength);
        var score = Math.Clamp(evaluation.Score, 0, 100);

        await _db.HouseListings
            .Where(l => l.Id == listingId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.AiScore, score)
                .SetProperty(l => l.AiSummary, summary)
                .SetProperty(l => l.AiProsJson, prosJson)
                .SetProperty(l => l.AiConsJson, consJson)
                .SetProperty(l => l.AiPriceAssessment, priceAssessment)
                .SetProperty(l => l.EvaluatedAt, DateTime.UtcNow)
                .SetProperty(l => l.UpdatedAt, DateTime.UtcNow),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<HouseListing>> GetTopListingsAsync(
        Guid profileId,
        int count,
        CancellationToken cancellationToken = default)
    {
        return await _db.HouseListings
            .AsNoTracking()
            .Where(l => l.SearchProfileId == profileId && !l.IsDeleted)
            .Where(l => l.AiScore != null)
            .OrderByDescending(l => l.AiScore)
            .ThenBy(l => l.Price / l.AreaSqm)
            .ThenBy(l => l.Id)
            .Take(count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<HouseListing> Listings, int TotalCount)> GetListingsAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        int? minScore,
        string? sortBy,
        CancellationToken cancellationToken = default)
    {
        var query = _db.HouseListings
            .AsNoTracking()
            .Where(l => l.SearchProfileId == profileId && !l.IsDeleted);

        if (minScore.HasValue)
        {
            query = query.Where(l => l.AiScore >= minScore.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        // Every ordering ends with the unique Id so ties are stable across pages;
        // otherwise SQL may return tied rows in a different order per request.
        var ordered = sortBy?.ToLowerInvariant() switch
        {
            "price" => query.OrderBy(l => l.Price),
            "firstseen" => query.OrderByDescending(l => l.FirstSeenAt),
            _ => query.OrderByDescending(l => l.AiScore ?? 0)
        };

        var listings = await ordered
            .ThenBy(l => l.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (listings, totalCount);
    }

    public async Task<IReadOnlyList<ScanRun>> GetScanHistoryAsync(
        Guid profileId,
        int count,
        CancellationToken cancellationToken = default)
    {
        return await _db.ScanRuns
            .AsNoTracking()
            .Where(s => s.SearchProfileId == profileId)
            .OrderByDescending(s => s.StartedAt)
            .Take(count)
            .Select(s => new ScanRun
            {
                Id = s.Id,
                SearchProfileId = s.SearchProfileId,
                Status = s.Status,
                StartedAt = s.StartedAt,
                CompletedAt = s.CompletedAt,
                ListingsFoundCount = s.ListingsFoundCount,
                NewListingsCount = s.NewListingsCount,
                PriceDropsCount = s.PriceDropsCount,
                EvaluatedCount = s.EvaluatedCount,
                Error = s.Error,
                // Exclude ReportHtml for list view; expose only its availability.
                HasReport = s.ReportHtml != null && s.ReportHtml != ""
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ScanRun?> GetScanRunByIdAsync(Guid scanRunId, CancellationToken cancellationToken = default)
    {
        return await _db.ScanRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == scanRunId, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    /// <summary>
    /// Serializes a list of strings to JSON that fits in <paramref name="maxLength"/>
    /// characters. Items are truncated and dropped from the end until it fits so
    /// the stored value is always valid JSON. Returns null for an empty list.
    /// </summary>
    internal static string? SerializeBounded(IReadOnlyList<string> items, int maxLength)
    {
        var cleaned = items
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => Truncate(i, maxLength / 4)!)
            .ToList();

        while (cleaned.Count > 0)
        {
            var json = JsonSerializer.Serialize(cleaned);
            if (json.Length <= maxLength)
                return json;

            cleaned.RemoveAt(cleaned.Count - 1);
        }

        return null;
    }
}
