using HomeSeeker.Abstractions;
using HomeSeeker.Configuration;
using HomeSeeker.Models;
using HomeSeeker.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ReceptyOks_UnitTests.HomeSeeker;

/// <summary>
/// Tests for <see cref="MarketScanService"/>: scraper isolation, candidate selection,
/// cost limits, ScanRun status transitions and the email rule.
/// </summary>
public sealed class MarketScanServiceTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();

    private Mock<IListingRepository> _repository = null!;
    private Mock<IListingEvaluator> _evaluator = null!;
    private Mock<IScanReportSender> _sender = null!;
    private ScanRun _scanRun = null!;
    private SearchProfile _profile = null!;
    private Dictionary<string, UpsertResult> _upsertResults = null!;

    [SetUp]
    public void SetUp()
    {
        _profile = new SearchProfile
        {
            Id = ProfileId,
            City = "Wrocław",
            NotificationEmail = "a@example.com",
            IsActive = true
        };
        _scanRun = new ScanRun { Id = Guid.NewGuid(), SearchProfileId = ProfileId, StartedAt = DateTime.UtcNow };
        _upsertResults = [];

        _repository = new Mock<IListingRepository>();
        _repository.Setup(r => r.GetProfileByIdAsync(ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _profile);
        _repository.Setup(r => r.CreateScanRunAsync(ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _scanRun);
        _repository.Setup(r => r.UpsertListingAsync(ProfileId, It.IsAny<ScrapedListing>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, ScrapedListing s, CancellationToken _) => _upsertResults[s.ExternalId]);
        _repository.Setup(r => r.GetTopListingsAsync(ProfileId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _evaluator = new Mock<IListingEvaluator>();
        _evaluator.Setup(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.IsAny<HouseListing>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListingEvaluation { Score = 70 });
        _evaluator.Setup(e => e.WriteReportHtmlAsync(It.IsAny<SearchProfile>(), It.IsAny<IReadOnlyList<HouseListing>>(),
                It.IsAny<ScanRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<html>report</html>");

        _sender = new Mock<IScanReportSender>();
        _sender.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    [Test]
    public async Task RunScan_WhenProfileMissing_ReturnsFalseWithoutCreatingScanRun()
    {
        _profile = null!;
        var sut = CreateSut([SucceedingScraper("Otodom")]);

        var result = await sut.RunScanAsync(ProfileId);

        Assert.That(result, Is.False);
        _repository.Verify(r => r.CreateScanRunAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RunScan_WhenProfileInactive_ReturnsFalseWithoutCreatingScanRun()
    {
        _profile.IsActive = false;
        var sut = CreateSut([SucceedingScraper("Otodom")]);

        var result = await sut.RunScanAsync(ProfileId);

        Assert.That(result, Is.False);
        _repository.Verify(r => r.CreateScanRunAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RunScan_WhenAllScrapersFail_MarksScanFailedAndSkipsEvaluation()
    {
        var failing = new Mock<IListingScraper>();
        failing.SetupGet(s => s.PortalName).Returns("Otodom");
        failing.Setup(s => s.SearchAsync(It.IsAny<SearchProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ScrapeResult.Failure("403"));

        var throwing = new Mock<IListingScraper>();
        throwing.SetupGet(s => s.PortalName).Returns("OLX");
        throwing.Setup(s => s.SearchAsync(It.IsAny<SearchProfile>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var sut = CreateSut([failing.Object, throwing.Object]);

        var result = await sut.RunScanAsync(ProfileId);

        Assert.That(result, Is.False);
        _repository.Verify(r => r.CompleteScanRunAsync(_scanRun.Id, ScanStatus.Failed, 0, 0, 0, 0, null,
            It.Is<string>(e => e.Contains("Otodom: 403") && e.Contains("OLX: boom")), It.IsAny<CancellationToken>()), Times.Once);
        _evaluator.Verify(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.IsAny<HouseListing>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _sender.VerifyNoOtherCalls();
    }

    [Test]
    public async Task RunScan_WhenOneScraperFails_StillCompletesWithOtherPortal()
    {
        AddUpsert("a", price: 500_000, area: 100, isNew: true);

        var failing = new Mock<IListingScraper>();
        failing.SetupGet(s => s.PortalName).Returns("OLX");
        failing.Setup(s => s.SearchAsync(It.IsAny<SearchProfile>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));

        var sut = CreateSut([SucceedingScraper("Otodom", "a"), failing.Object]);

        var result = await sut.RunScanAsync(ProfileId);

        Assert.That(result, Is.True);
        _repository.Verify(r => r.CompleteScanRunAsync(_scanRun.Id, ScanStatus.Completed, 1, 1, 0, 1,
            "<html>report</html>", It.Is<string>(e => e.StartsWith("Niepełny skan") && e.Contains("OLX: down")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunScan_SelectsOnlyNewOrPriceDropped_OrderedByPricePerSqm_LimitedByOptions()
    {
        // price/m²: cheap=3000, mid=5000, dropped=4000, expensive=9000, unchanged=1000 (not a candidate)
        AddUpsert("cheap", price: 300_000, area: 100, isNew: true);
        AddUpsert("mid", price: 500_000, area: 100, isNew: true);
        AddUpsert("dropped", price: 400_000, area: 100, priceDropped: true);
        AddUpsert("expensive", price: 900_000, area: 100, isNew: true);
        AddUpsert("unchanged", price: 100_000, area: 100);

        var evaluatedIds = new List<string>();
        _evaluator.Setup(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.IsAny<HouseListing>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback((SearchProfile _, HouseListing l, string? _, CancellationToken _) => evaluatedIds.Add(l.ExternalId))
            .ReturnsAsync(new ListingEvaluation { Score = 50 });

        var sut = CreateSut([SucceedingScraper("Otodom", "cheap", "mid", "dropped", "expensive", "unchanged")],
            maxCandidates: 3);

        await sut.RunScanAsync(ProfileId);

        Assert.That(evaluatedIds, Is.EqualTo(new[] { "cheap", "dropped", "mid" }));
        _repository.Verify(r => r.CompleteScanRunAsync(_scanRun.Id, ScanStatus.Completed, 5, 3, 1, 3,
            It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunScan_WithBothPropertyTypes_SharesCandidateSlotsBetweenTypes()
    {
        // Houses are cheaper per m², so a global ranking would pick only houses.
        AddUpsert("h1", price: 200_000, area: 100, isNew: true);
        AddUpsert("h2", price: 210_000, area: 100, isNew: true);
        AddUpsert("h3", price: 220_000, area: 100, isNew: true);
        AddUpsert("a1", price: 900_000, area: 100, isNew: true, type: PropertyTypes.Apartment);
        AddUpsert("a2", price: 950_000, area: 100, isNew: true, type: PropertyTypes.Apartment);

        var evaluatedIds = new List<string>();
        _evaluator.Setup(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.IsAny<HouseListing>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback((SearchProfile _, HouseListing l, string? _, CancellationToken _) => evaluatedIds.Add(l.ExternalId))
            .ReturnsAsync(new ListingEvaluation { Score = 50 });

        var sut = CreateSut([SucceedingScraper("Otodom", "h1", "h2", "h3", "a1", "a2")], maxCandidates: 4);

        await sut.RunScanAsync(ProfileId);

        Assert.That(evaluatedIds, Is.EquivalentTo(new[] { "h1", "h2", "a1", "a2" }));
    }

    [Test]
    public void SelectCandidates_WhenOneTypeHasFewListings_GivesUnusedSlotsToOtherType()
    {
        AddUpsert("h1", price: 200_000, area: 100, isNew: true);
        AddUpsert("h2", price: 210_000, area: 100, isNew: true);
        AddUpsert("h3", price: 220_000, area: 100, isNew: true);
        AddUpsert("a1", price: 900_000, area: 100, isNew: true, type: PropertyTypes.Apartment);

        var selected = MarketScanService.SelectCandidates(_upsertResults.Values, 4);

        Assert.That(selected.Select(r => r.Listing.ExternalId), Is.EquivalentTo(new[] { "h1", "h2", "h3", "a1" }));
    }

    [Test]
    public async Task RunScan_WhenScraperReportsPartialResult_CompletesAndRecordsWarning()
    {
        AddUpsert("a", price: 500_000, area: 100, isNew: true);

        var partial = new Mock<IListingScraper>();
        partial.SetupGet(s => s.PortalName).Returns("OLX");
        partial.Setup(s => s.SearchAsync(It.IsAny<SearchProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ScrapeResult.Success(
                [new ScrapedListing { Portal = "OLX", ExternalId = "a", Url = "https://example.com/a", Title = "a", Price = 1, AreaSqm = 1 }],
                ["mieszkanie - nie pobrano"]));

        var sut = CreateSut([partial.Object]);

        var result = await sut.RunScanAsync(ProfileId);

        Assert.That(result, Is.True);
        _repository.Verify(r => r.CompleteScanRunAsync(_scanRun.Id, ScanStatus.Completed, 1, 1, 0, 1,
            It.IsAny<string>(), It.Is<string>(e => e.Contains("OLX: mieszkanie - nie pobrano")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunScan_PassesDetailsFromSearchResults_WithoutFetchingPage()
    {
        AddUpsert("a", price: 300_000, area: 100, isNew: true);

        var scraper = new Mock<IListingScraper>();
        scraper.SetupGet(s => s.PortalName).Returns("Otodom");
        scraper.Setup(s => s.SearchAsync(It.IsAny<SearchProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ScrapeResult.Success(
                [new ScrapedListing { Portal = "Otodom", ExternalId = "a", Url = "https://x/a", Title = "a", Price = 1, AreaSqm = 1, Details = "Opis z listy" }]));

        var sut = CreateSut([scraper.Object]);

        await sut.RunScanAsync(ProfileId);

        _evaluator.Verify(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.IsAny<HouseListing>(), "Opis z listy", It.IsAny<CancellationToken>()), Times.Once);
        scraper.Verify(s => s.GetDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RunScan_FetchesDetailsForCandidates_FromMatchingPortal()
    {
        AddUpsert("a", price: 300_000, area: 100, isNew: true);
        AddUpsert("old", price: 300_000, area: 100);

        var scraper = Mock.Get(SucceedingScraper("Otodom", "a", "old"));
        scraper.Setup(s => s.GetDetailsAsync(_upsertResults["a"].Listing.Url, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Rok budowy: 2009");

        var sut = CreateSut([scraper.Object]);

        await sut.RunScanAsync(ProfileId);

        _evaluator.Verify(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.Is<HouseListing>(l => l.ExternalId == "a"),
            "Rok budowy: 2009", It.IsAny<CancellationToken>()), Times.Once);
        // Only AI candidates get the extra request.
        scraper.Verify(s => s.GetDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunScan_WhenDetailsFetchThrows_StillEvaluatesWithoutDetails()
    {
        AddUpsert("a", price: 300_000, area: 100, isNew: true);

        var scraper = Mock.Get(SucceedingScraper("Otodom", "a"));
        scraper.Setup(s => s.GetDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));

        var sut = CreateSut([scraper.Object]);

        var result = await sut.RunScanAsync(ProfileId);

        Assert.That(result, Is.True);
        _evaluator.Verify(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.IsAny<HouseListing>(), null, It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.SaveEvaluationAsync(It.IsAny<Guid>(), It.IsAny<ListingEvaluation>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunScan_WhenEvaluationReturnsNullOrThrows_SkipsListingAndContinues()
    {
        AddUpsert("null", price: 100_000, area: 100, isNew: true);
        AddUpsert("throws", price: 200_000, area: 100, isNew: true);
        AddUpsert("ok", price: 300_000, area: 100, isNew: true);

        _evaluator.Setup(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.Is<HouseListing>(l => l.ExternalId == "null"), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ListingEvaluation?)null);
        _evaluator.Setup(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.Is<HouseListing>(l => l.ExternalId == "throws"), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("AI down"));

        var sut = CreateSut([SucceedingScraper("Otodom", "null", "throws", "ok")]);

        var result = await sut.RunScanAsync(ProfileId);

        Assert.That(result, Is.True);
        _repository.Verify(r => r.SaveEvaluationAsync(_upsertResults["ok"].Listing.Id, It.IsAny<ListingEvaluation>(), It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.SaveEvaluationAsync(It.IsAny<Guid>(), It.IsAny<ListingEvaluation>(), It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.ResetTracking(), Times.Once);
        _repository.Verify(r => r.CompleteScanRunAsync(_scanRun.Id, ScanStatus.Completed, 3, 3, 0, 1,
            It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunScan_WhenUpsertFails_ResetsTrackingAndContinues()
    {
        AddUpsert("ok", price: 300_000, area: 100, isNew: true);
        _repository.Setup(r => r.UpsertListingAsync(ProfileId, It.Is<ScrapedListing>(s => s.ExternalId == "bad"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db"));

        var sut = CreateSut([SucceedingScraper("Otodom", "bad", "ok")]);

        var result = await sut.RunScanAsync(ProfileId);

        Assert.That(result, Is.True);
        _repository.Verify(r => r.ResetTracking(), Times.Once);
        _repository.Verify(r => r.CompleteScanRunAsync(_scanRun.Id, ScanStatus.Completed, 2, 1, 0, 1,
            It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunScan_WithNewListings_SendsEmailWithCounts()
    {
        AddUpsert("a", price: 300_000, area: 100, isNew: true);
        AddUpsert("b", price: 300_000, area: 100, priceDropped: true);

        var sut = CreateSut([SucceedingScraper("Otodom", "a", "b")]);

        await sut.RunScanAsync(ProfileId);

        _sender.Verify(s => s.SendAsync("a@example.com",
            It.Is<string>(subj => subj.Contains("Wrocław") && subj.Contains("1 nowych") && subj.Contains("1 obniżek")),
            "<html>report</html>", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunScan_WithoutNewListingsOrDrops_DoesNotSendEmailButStoresReport()
    {
        AddUpsert("a", price: 300_000, area: 100);

        var sut = CreateSut([SucceedingScraper("Otodom", "a")]);

        var result = await sut.RunScanAsync(ProfileId);

        Assert.That(result, Is.True);
        _sender.VerifyNoOtherCalls();
        _repository.Verify(r => r.CompleteScanRunAsync(_scanRun.Id, ScanStatus.Completed, 1, 0, 0, 0,
            "<html>report</html>", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunScan_WhenEmailFails_ScanStillCompletes()
    {
        AddUpsert("a", price: 300_000, area: 100, isNew: true);
        _sender.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = CreateSut([SucceedingScraper("Otodom", "a")]);

        var result = await sut.RunScanAsync(ProfileId);

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task RunScan_WhenCancelled_MarksFailedUsingNonCancelledToken()
    {
        using var cts = new CancellationTokenSource();

        var scraper = new Mock<IListingScraper>();
        scraper.SetupGet(s => s.PortalName).Returns("Otodom");
        scraper.Setup(s => s.SearchAsync(It.IsAny<SearchProfile>(), It.IsAny<CancellationToken>()))
            .Returns(async (SearchProfile _, CancellationToken ct) =>
            {
                await cts.CancelAsync();
                ct.ThrowIfCancellationRequested();
                return ScrapeResult.Success([]);
            });

        CancellationToken finalToken = default;
        _repository.Setup(r => r.CompleteScanRunAsync(It.IsAny<Guid>(), It.IsAny<ScanStatus>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback((Guid _, ScanStatus _, int _, int _, int _, int _, string? _, string? _, CancellationToken ct) => finalToken = ct)
            .Returns(Task.CompletedTask);

        var sut = CreateSut([scraper.Object]);

        var result = await sut.RunScanAsync(ProfileId, cts.Token);

        Assert.That(result, Is.False);
        Assert.That(finalToken.IsCancellationRequested, Is.False);
        _repository.Verify(r => r.CompleteScanRunAsync(_scanRun.Id, ScanStatus.Failed, It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), "Scan cancelled", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunScan_WhenFinalStatusWriteFails_ReturnsFalse()
    {
        AddUpsert("a", price: 300_000, area: 100);
        _repository.Setup(r => r.CompleteScanRunAsync(It.IsAny<Guid>(), It.IsAny<ScanStatus>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db unreachable"));

        var sut = CreateSut([SucceedingScraper("Otodom", "a")]);

        var result = await sut.RunScanAsync(ProfileId);

        Assert.That(result, Is.False);
    }

    private MarketScanService CreateSut(IEnumerable<IListingScraper> scrapers, int maxCandidates = 8) =>
        new(
            scrapers,
            _repository.Object,
            _evaluator.Object,
            _sender.Object,
            Options.Create(new HomeSeekerOptions { MaxCandidatesPerScan = maxCandidates, TopListingsInReport = 5, RequestDelay = TimeSpan.Zero }),
            NullLogger<MarketScanService>.Instance);

    private void AddUpsert(string externalId, decimal price, decimal area, bool isNew = false, bool priceDropped = false,
        PropertyTypes type = PropertyTypes.House)
    {
        _upsertResults[externalId] = new UpsertResult
        {
            Listing = new HouseListing
            {
                Id = Guid.NewGuid(),
                SearchProfileId = ProfileId,
                Portal = "Otodom",
                ExternalId = externalId,
                Url = $"https://www.otodom.pl/pl/oferta/{externalId}",
                Title = externalId,
                Price = price,
                AreaSqm = area,
                PropertyType = type
            },
            IsNew = isNew,
            PriceDropped = priceDropped
        };
    }

    private static IListingScraper SucceedingScraper(string portal, params string[] externalIds)
    {
        var listings = externalIds.Select(id => new ScrapedListing
        {
            Portal = portal,
            ExternalId = id,
            Url = $"https://example.com/{id}",
            Title = id,
            Price = 1,
            AreaSqm = 1
        }).ToList();

        var scraper = new Mock<IListingScraper>();
        scraper.SetupGet(s => s.PortalName).Returns(portal);
        scraper.Setup(s => s.SearchAsync(It.IsAny<SearchProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ScrapeResult.Success(listings));
        return scraper.Object;
    }
}
