using HomeSeeker.Configuration;
using HomeSeeker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReceptyOks.Api.DbUtility;
using ReceptyOks.Api.Middleware;

namespace ReceptyOks_UnitTests.HomeSeeker;

[TestFixture]
public class HomeSeekerCleanerTests
{
    private ServiceProvider _serviceProvider = null!;
    private string _dbPath = null!;

    private static readonly HomeSeekerOptions Options = new()
    {
        ListingRetention = TimeSpan.FromDays(30),
        ScanHistoryRetention = TimeSpan.FromDays(90)
    };

    [SetUp]
    public void SetUp()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"hs_cleaner_test_{Guid.NewGuid():N}.db");

        var services = new ServiceCollection();
        services.AddDbContext<HomeSeekerDbContext>(opts => opts.UseSqlite($"DataSource={_dbPath}"));
        _serviceProvider = services.BuildServiceProvider();

        using var scope = _serviceProvider.CreateScope();
        scope.ServiceProvider.GetRequiredService<HomeSeekerDbContext>().Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch (IOException) { }
    }

    [Test]
    public async Task WhenListingUnseenBeyondRetentionAndProfileRecentlyScannedThenItIsDeleted()
    {
        var profileId = await SeedProfileAsync(lastScannedDaysAgo: 1);
        await SeedListingAsync(profileId, "stale", lastSeenDaysAgo: 40);
        await SeedListingAsync(profileId, "fresh", lastSeenDaysAgo: 5);

        var result = await CreateSut().CleanupAsync(CancellationToken.None);

        Assert.That(result.Listings, Is.EqualTo(1));
        Assert.That(await RemainingListingIdsAsync(), Is.EqualTo(new[] { "fresh" }));
    }

    [Test]
    public async Task WhenProfileNotScannedWithinRetentionThenListingsAreKept()
    {
        var profileId = await SeedProfileAsync(lastScannedDaysAgo: 60);
        await SeedListingAsync(profileId, "old", lastSeenDaysAgo: 60);

        var result = await CreateSut().CleanupAsync(CancellationToken.None);

        Assert.That(result.Listings, Is.Zero);
        Assert.That(await RemainingListingIdsAsync(), Is.EqualTo(new[] { "old" }));
    }

    [Test]
    public async Task WhenProfileNeverScannedThenListingsAreKept()
    {
        var profileId = await SeedProfileAsync(lastScannedDaysAgo: null);
        await SeedListingAsync(profileId, "old", lastSeenDaysAgo: 60);

        var result = await CreateSut().CleanupAsync(CancellationToken.None);

        Assert.That(result.Listings, Is.Zero);
    }

    [Test]
    public async Task WhenProfileIsPausedThenStaleListingsAreKept()
    {
        var profileId = await SeedProfileAsync(lastScannedDaysAgo: 1, isActive: false);
        await SeedListingAsync(profileId, "stale", lastSeenDaysAgo: 40);
        await SeedListingAsync(profileId, "fresh", lastSeenDaysAgo: 5);

        var result = await CreateSut().CleanupAsync(CancellationToken.None);

        Assert.That(result.Listings, Is.Zero);
    }

    [Test]
    public async Task WhenListingsPortalWasNotReadWithinRetentionThenItsListingsAreKept()
    {
        // Otodom keeps working (fresh listing), OLX has been blocked for the whole window.
        var profileId = await SeedProfileAsync(lastScannedDaysAgo: 1);
        await SeedListingAsync(profileId, "otodom-fresh", lastSeenDaysAgo: 1);
        await SeedListingAsync(profileId, "otodom-stale", lastSeenDaysAgo: 40);
        await SeedListingAsync(profileId, "olx-stale", lastSeenDaysAgo: 40, portal: "OLX");

        var result = await CreateSut().CleanupAsync(CancellationToken.None);

        Assert.That(result.Listings, Is.EqualTo(1));
        Assert.That(await RemainingListingIdsAsync(), Is.EquivalentTo(new[] { "otodom-fresh", "olx-stale" }));
    }

    [Test]
    public async Task WhenScanRunOlderThanHistoryRetentionThenItIsDeleted()
    {
        var profileId = await SeedProfileAsync(lastScannedDaysAgo: 1);
        await SeedScanRunAsync(profileId, startedDaysAgo: 100);
        var recentId = await SeedScanRunAsync(profileId, startedDaysAgo: 10);

        var result = await CreateSut().CleanupAsync(CancellationToken.None);

        Assert.That(result.ScanRuns, Is.EqualTo(1));
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HomeSeekerDbContext>();
        Assert.That(await db.ScanRuns.Select(s => s.Id).ToListAsync(), Is.EqualTo(new[] { recentId }));
    }

    private HomeSeekerCleaner CreateSut() => new(
        _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
        Microsoft.Extensions.Options.Options.Create(Options),
        NullLogger<HomeSeekerCleaner>.Instance);

    private async Task<Guid> SeedProfileAsync(int? lastScannedDaysAgo, bool isActive = true)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HomeSeekerDbContext>();
        var profile = new SearchProfile
        {
            Id = Guid.NewGuid(),
            City = "Kraków",
            NotificationEmail = "a@b.pl",
            IsActive = isActive,
            LastScannedAt = lastScannedDaysAgo is { } d ? DateTime.UtcNow.AddDays(-d) : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.SearchProfiles.Add(profile);
        await db.SaveChangesAsync();
        return profile.Id;
    }

    private async Task SeedListingAsync(Guid profileId, string externalId, int lastSeenDaysAgo, string portal = "Otodom")
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HomeSeekerDbContext>();
        var seen = DateTime.UtcNow.AddDays(-lastSeenDaysAgo);
        db.HouseListings.Add(new HouseListing
        {
            Id = Guid.NewGuid(),
            SearchProfileId = profileId,
            Portal = portal,
            ExternalId = externalId,
            Url = $"https://www.otodom.pl/pl/oferta/{externalId}",
            Title = externalId,
            Price = 500_000,
            AreaSqm = 50,
            FirstSeenAt = seen,
            LastSeenAt = seen,
            CreatedAt = seen,
            UpdatedAt = seen
        });
        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedScanRunAsync(Guid profileId, int startedDaysAgo)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HomeSeekerDbContext>();
        var run = new ScanRun
        {
            Id = Guid.NewGuid(),
            SearchProfileId = profileId,
            Status = ScanStatus.Completed,
            StartedAt = DateTime.UtcNow.AddDays(-startedDaysAgo),
            CompletedAt = DateTime.UtcNow.AddDays(-startedDaysAgo)
        };
        db.ScanRuns.Add(run);
        await db.SaveChangesAsync();
        return run.Id;
    }

    private async Task<List<string>> RemainingListingIdsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HomeSeekerDbContext>();
        return await db.HouseListings.Select(l => l.ExternalId).ToListAsync();
    }
}
