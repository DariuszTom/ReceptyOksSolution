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

    public async Task<IReadOnlyList<SearchProfile>> GetActiveProfilesAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - _options.ScanInterval;

        return await _db.SearchProfiles
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.IsActive)
            .Where(p => p.LastScannedAt == null || p.LastScannedAt < cutoff)
            .OrderBy(p => p.LastScannedAt ?? DateTime.MinValue)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<SearchProfile?> GetProfileByIdAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        return await _db.SearchProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == profileId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> TryMarkProfileScannedAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        // Update-first approach for distributed locking:
        // Only update if LastScannedAt is still old (another instance hasn't started)
        var cutoff = DateTime.UtcNow - _options.ScanInterval;
        var now = DateTime.UtcNow;

        var updated = await _db.SearchProfiles
            .Where(p => p.Id == profileId)
            .Where(p => p.LastScannedAt == null || p.LastScannedAt < cutoff)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.LastScannedAt, now)
                .SetProperty(p => p.UpdatedAt, now),
                cancellationToken)
            .ConfigureAwait(false);

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

    public async Task SaveEvaluationAsync(
        Guid listingId,
        ListingEvaluation evaluation,
        CancellationToken cancellationToken = default)
    {
        var prosJson = evaluation.Pros.Count > 0
            ? JsonSerializer.Serialize(evaluation.Pros)
            : null;

        var consJson = evaluation.Cons.Count > 0
            ? JsonSerializer.Serialize(evaluation.Cons)
            : null;

        await _db.HouseListings
            .Where(l => l.Id == listingId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.AiScore, evaluation.Score)
                .SetProperty(l => l.AiSummary, evaluation.Summary)
                .SetProperty(l => l.AiProsJson, prosJson)
                .SetProperty(l => l.AiConsJson, consJson)
                .SetProperty(l => l.AiPriceAssessment, evaluation.PriceAssessment)
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

        query = sortBy?.ToLowerInvariant() switch
        {
            "score" => query.OrderByDescending(l => l.AiScore ?? 0),
            "price" => query.OrderBy(l => l.Price),
            "firstseen" => query.OrderByDescending(l => l.FirstSeenAt),
            _ => query.OrderByDescending(l => l.AiScore ?? 0)
        };

        var listings = await query
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
                Error = s.Error
                // Exclude ReportHtml for list view
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

    public async Task<SearchProfile> CreateProfileAsync(SearchProfileRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var profile = new SearchProfile
        {
            Id = Guid.NewGuid(),
            City = request.City,
            District = request.District,
            MinPrice = request.MinPrice,
            MaxPrice = request.MaxPrice,
            MinAreaSqm = request.MinAreaSqm,
            MaxAreaSqm = request.MaxAreaSqm,
            ExtraCriteria = request.ExtraCriteria,
            NotificationEmail = request.NotificationEmail,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.SearchProfiles.Add(profile);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return profile;
    }

    public async Task<SearchProfile?> UpdateProfileAsync(
        Guid profileId,
        SearchProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var profile = await _db.SearchProfiles
            .FirstOrDefaultAsync(p => p.Id == profileId && !p.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        if (profile is null)
            return null;

        profile.City = request.City;
        profile.District = request.District;
        profile.MinPrice = request.MinPrice;
        profile.MaxPrice = request.MaxPrice;
        profile.MinAreaSqm = request.MinAreaSqm;
        profile.MaxAreaSqm = request.MaxAreaSqm;
        profile.ExtraCriteria = request.ExtraCriteria;
        profile.NotificationEmail = request.NotificationEmail;
        profile.IsActive = request.IsActive;
        profile.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return profile;
    }

    public async Task<bool> DeleteProfileAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var updated = await _db.SearchProfiles
            .Where(p => p.Id == profileId && !p.IsDeleted)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.IsDeleted, true)
                .SetProperty(p => p.UpdatedAt, DateTime.UtcNow),
                cancellationToken)
            .ConfigureAwait(false);

        return updated > 0;
    }

    public async Task<IReadOnlyList<SearchProfile>> GetAllProfilesAsync(CancellationToken cancellationToken = default)
    {
        return await _db.SearchProfiles
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
