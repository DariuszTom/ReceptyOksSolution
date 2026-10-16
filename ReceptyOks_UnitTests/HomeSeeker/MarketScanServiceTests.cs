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
        _evaluator.Setup(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.IsAny<HouseListing>(), It.IsAny<string?>(), It.IsAny<PriceBenchmark?>(), It.IsAny<CancellationToken>()))
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
        _evaluator.Verify(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.IsAny<HouseListing>(), It.IsAny<string?>(), It.IsAny<PriceBenchmark?>(), It.IsAny<CancellationToken>()), Times.Never);
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
        _evaluator.Setup(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.IsAny<HouseListing>(), It.IsAny<string?>(), It.IsAny<PriceBenchmark?>(), It.IsAny<CancellationToken>()))
            .Callback((SearchProfile _, HouseListing l, string? _, PriceBenchmark? _, CancellationToken _) => evaluatedIds.Add(l.ExternalId))
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
        _evaluator.Setup(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.IsAny<HouseListing>(), It.IsAny<string?>(), It.IsAny<PriceBenchmark?>(), It.IsAny<CancellationToken>()))
            .Callback((SearchProfile _, HouseListing l, string? _, PriceBenchmark? _, CancellationToken _) => evaluatedIds.Add(l.ExternalId))
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

        var selected = ListingSelection.SelectCandidates(_upsertResults.Values, 4);

        Assert.That(selected.Select(r => r.Listing.ExternalId), Is.EquivalentTo(new[] { "h1", "h2", "h3", "a1" }));
    }

    [TestCase(1_000_000, 100, true)]
    [TestCase(2_500_000, 410, false)]  // the Otodom "similar offers" case
    [TestCase(1_500_000, 100, false)]
    [TestCase(600_000, 100, false)]
    [TestCase(1_000_000, 60, false)]
    [TestCase(1_000_000, 140, false)]
    [TestCase(0, 100, true)]           // price unknown - keep
    [TestCase(1_000_000, 0, true)]     // area unknown - keep
    public void MatchesCriteria_EnforcesProfileRanges(decimal price, decimal area, bool expected)
    {
        var profile = new SearchProfile { City = "Kraków", MinPrice = 700_000, MaxPrice = 1_400_000, MinAreaSqm = 70, MaxAreaSqm = 130 };
        var listing = new ScrapedListing { Portal = "Otodom", ExternalId = "x", Url = "https://x", Title = "x", Price = price, AreaSqm = area };

        Assert.That(ListingSelection.MatchesCriteria(profile, listing), Is.EqualTo(expected));
    }

    [TestCase(4, 4, true)]
    [TestCase(4, 5, true)]
    [TestCase(4, 3, false)]
    [TestCase(4, null, true)]   // rooms unknown - keep
    [TestCase(null, 1, true)]   // no requirement
    public void MatchesCriteria_EnforcesMinRooms(int? minRooms, int? rooms, bool expected)
    {
        var profile = new SearchProfile { City = "Kraków", MinRooms = minRooms };
        var listing = new ScrapedListing { Portal = "Otodom", ExternalId = "x", Url = "https://x", Title = "x", Price = 1, AreaSqm = 1, Rooms = rooms };

        Assert.That(ListingSelection.MatchesCriteria(profile, listing), Is.EqualTo(expected));
    }

    [Test]
    public async Task RunScan_DropsListingsOutsideProfileRangeBeforeStoring()
    {
        _profile.MaxPrice = 1_400_000;
        AddUpsert("ok", price: 1_000_000, area: 100, isNew: true);
        var scraper = new Mock<IListingScraper>();
        scraper.SetupGet(s => s.PortalName).Returns("Otodom");
        scraper.Setup(s => s.SearchAsync(It.IsAny<SearchProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ScrapeResult.Success(
            [
                new ScrapedListing { Portal = "Otodom", ExternalId = "ok", Url = "https://x/ok", Title = "ok", Price = 1_000_000, AreaSqm = 100 },
                new ScrapedListing { Portal = "Otodom", ExternalId = "too-expensive", Url = "https://x/te", Title = "te", Price = 2_500_000, AreaSqm = 100 }
            ]));

        var result = await CreateSut([scraper.Object]).RunScanAsync(ProfileId);

        Assert.That(result, Is.True);
        _repository.Verify(r => r.UpsertListingAsync(ProfileId, It.Is<ScrapedListing>(s => s.ExternalId == "too-expensive"), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.UpsertListingAsync(ProfileId, It.Is<ScrapedListing>(s => s.ExternalId == "ok"), It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.ExcludeListingAsync(ProfileId, It.Is<ScrapedListing>(s => s.ExternalId == "too-expensive" && s.Price == 2_500_000), It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.ExcludeListingAsync(ProfileId, It.Is<ScrapedListing>(s => s.ExternalId == "ok"), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void SelectCandidates_DeduplicatesRepeatedListings_PreferringNewOccurrence()
    {
        AddUpsert("dup", price: 300_000, area: 100, isNew: true);
        var first = _upsertResults["dup"];
        var repeated = new UpsertResult { Listing = first.Listing, IsNew = false, PriceDropped = false };
        AddUpsert("other", price: 900_000, area: 100);

        var selected = ListingSelection.SelectCandidates([repeated, first, _upsertResults["other"]], 3);

        Assert.That(selected.Select(r => r.Listing.ExternalId), Is.EqualTo(new[] { "dup", "other" }));
        Assert.That(selected[0].IsNew, Is.True);
    }

    [Test]
    public void SelectCandidates_FillsSpareSlotsWithUnevaluatedBacklog()
    {
        AddUpsert("new", price: 900_000, area: 100, isNew: true);
        AddUpsert("backlog-cheap", price: 300_000, area: 100);
        AddUpsert("backlog-pricey", price: 800_000, area: 100);
        AddUpsert("already-scored", price: 100_000, area: 100);
        _upsertResults["already-scored"].Listing.AiScore = 70;

        var selected = ListingSelection.SelectCandidates(_upsertResults.Values, 2);

        // New listings come first; the cheapest unevaluated one fills the remaining slot.
        Assert.That(selected.Select(r => r.Listing.ExternalId), Is.EqualTo(new[] { "new", "backlog-cheap" }));
    }

    [Test]
    public async Task RunScan_WithOnlyUnevaluatedBacklog_EvaluatesItWithoutSendingEmail()
    {
        AddUpsert("backlog", price: 300_000, area: 100);

        var result = await CreateSut([SucceedingScraper("Otodom", "backlog")]).RunScanAsync(ProfileId);

        Assert.That(result, Is.True);
        _evaluator.Verify(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.Is<HouseListing>(l => l.ExternalId == "backlog"),
            It.IsAny<string?>(), It.IsAny<PriceBenchmark?>(), It.IsAny<CancellationToken>()), Times.Once);
        _sender.VerifyNoOtherCalls();
    }

    [Test]
    public void SelectCandidates_RanksByPriceRelativeToDistrictMedian()
    {
        // Outskirts: median 8 000 zł/m²; "outskirts-typical" is cheap in absolute terms but average locally.
        for (var i = 0; i < 5; i++)
            AddUpsert($"o{i}", price: 800_000 + i * 10_000, area: 100, location: "Kraków, Swoszowice");
        AddUpsert("outskirts-typical", price: 800_000, area: 100, isNew: true, location: "Kraków, Swoszowice");

        // Centre: median 18 000 zł/m²; "centre-bargain" is 14 000 zł/m² (~22% below local median).
        for (var i = 0; i < 5; i++)
            AddUpsert($"c{i}", price: 1_800_000 + i * 10_000, area: 100, location: "Kraków, Kazimierz");
        AddUpsert("centre-bargain", price: 1_400_000, area: 100, isNew: true, location: "Kraków, kazimierz");

        var selected = ListingSelection.SelectCandidates(_upsertResults.Values, 1);

        Assert.That(selected.Single().Listing.ExternalId, Is.EqualTo("centre-bargain"));
    }

    [Test]
    public async Task RunScan_PassesDistrictBenchmarkToEvaluator()
    {
        for (var i = 0; i < 5; i++)
            AddUpsert($"o{i}", price: 800_000, area: 100, location: "Kraków, Swoszowice");
        AddUpsert("new", price: 700_000, area: 100, isNew: true, location: "Kraków, Swoszowice");

        PriceBenchmark? captured = null;
        _evaluator.Setup(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.IsAny<HouseListing>(), It.IsAny<string?>(), It.IsAny<PriceBenchmark?>(), It.IsAny<CancellationToken>()))
            .Callback((SearchProfile _, HouseListing _, string? _, PriceBenchmark? b, CancellationToken _) => captured = b)
            .ReturnsAsync(new ListingEvaluation { Score = 50 });

        var sut = CreateSut([SucceedingScraper("Otodom", "o0", "o1", "o2", "o3", "o4", "new")]);

        await sut.RunScanAsync(ProfileId);

        Assert.That(captured, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(captured!.DistrictMedianPerSqm, Is.EqualTo(8_000m));
            Assert.That(captured.DistrictSampleSize, Is.EqualTo(6));
            Assert.That(captured.CitySampleSize, Is.EqualTo(6));
        });
    }

    [Test]
    public async Task RunScan_UsesListingsFromPreviousScansForDistrictMedian()
    {
        // Only one listing in this scan; four more from earlier scans make the district reliable.
        AddUpsert("new", price: 700_000, area: 100, isNew: true, location: "Kraków, Swoszowice");
        var history = Enumerable.Range(0, 4).Select(_ => new HouseListing
        {
            Id = Guid.NewGuid(), Price = 900_000, AreaSqm = 100, Location = "Kraków, Swoszowice"
        }).ToList();
        // The current listing also comes back from the DB (already upserted) - must not be counted twice.
        history.Add(_upsertResults["new"].Listing);

        DateTime? seenSince = null;
        _repository.Setup(r => r.GetMarketSampleAsync(ProfileId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback((Guid _, DateTime since, CancellationToken _) => seenSince = since)
            .ReturnsAsync(history);

        PriceBenchmark? captured = null;
        _evaluator.Setup(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.IsAny<HouseListing>(), It.IsAny<string?>(), It.IsAny<PriceBenchmark?>(), It.IsAny<CancellationToken>()))
            .Callback((SearchProfile _, HouseListing _, string? _, PriceBenchmark? b, CancellationToken _) => captured = b)
            .ReturnsAsync(new ListingEvaluation { Score = 50 });

        await CreateSut([SucceedingScraper("Otodom", "new")]).RunScanAsync(ProfileId);

        Assert.Multiple(() =>
        {
            Assert.That(captured!.DistrictSampleSize, Is.EqualTo(5));
            Assert.That(captured.DistrictMedianPerSqm, Is.EqualTo(9_000m));
            Assert.That(seenSince, Is.EqualTo(DateTime.UtcNow.AddDays(-30)).Within(TimeSpan.FromMinutes(1)));
        });
    }

    [Test]
    public async Task RunScan_IgnoresHistoryFromOtherCitiesForMedians()
    {
        // Profile city changed from Warszawa to Kraków: old Warszawa listings must not
        // pollute the Kraków medians (district keys have no city, e.g. "centrum").
        AddUpsert("new", price: 700_000, area: 100, isNew: true, location: "Kraków, Centrum");
        var history = Enumerable.Range(0, 5).Select(_ => new HouseListing
        {
            Id = Guid.NewGuid(), Price = 2_000_000, AreaSqm = 100, Location = "Warszawa, Centrum"
        }).ToList();
        history.Add(new HouseListing { Id = Guid.NewGuid(), Price = 900_000, AreaSqm = 100, Location = "kraków, Podgórze" });

        _repository.Setup(r => r.GetMarketSampleAsync(ProfileId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(history);

        PriceBenchmark? captured = null;
        _evaluator.Setup(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.IsAny<HouseListing>(), It.IsAny<string?>(), It.IsAny<PriceBenchmark?>(), It.IsAny<CancellationToken>()))
            .Callback((SearchProfile _, HouseListing _, string? _, PriceBenchmark? b, CancellationToken _) => captured = b)
            .ReturnsAsync(new ListingEvaluation { Score = 50 });

        await CreateSut([SucceedingScraper("Otodom", "new")]).RunScanAsync(ProfileId);

        Assert.Multiple(() =>
        {
            Assert.That(captured!.CitySampleSize, Is.EqualTo(2));
            Assert.That(captured.CityMedianPerSqm, Is.EqualTo(8_000m));
            Assert.That(captured.DistrictMedianPerSqm, Is.Null);
        });
    }

    [Test]
    public async Task RunScan_WhenMarketSampleFails_UsesCurrentScanOnly()
    {
        AddUpsert("new", price: 700_000, area: 100, isNew: true, location: "Kraków, Swoszowice");
        _repository.Setup(r => r.GetMarketSampleAsync(ProfileId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db"));

        PriceBenchmark? captured = null;
        _evaluator.Setup(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.IsAny<HouseListing>(), It.IsAny<string?>(), It.IsAny<PriceBenchmark?>(), It.IsAny<CancellationToken>()))
            .Callback((SearchProfile _, HouseListing _, string? _, PriceBenchmark? b, CancellationToken _) => captured = b)
            .ReturnsAsync(new ListingEvaluation { Score = 50 });

        var result = await CreateSut([SucceedingScraper("Otodom", "new")]).RunScanAsync(ProfileId);

        Assert.That(result, Is.True);
        Assert.That(captured!.CitySampleSize, Is.EqualTo(1));
    }

    [Test]
    public void MarketPriceStats_WhenDistrictHasFewListings_FallsBackToCityMedian()
    {
        var listings = new[]
        {
            Listing(5_000, "Kraków, Podgórze"), Listing(6_000, "Kraków, Podgórze"),
            Listing(7_000, "Kraków, Nowa Huta"), Listing(8_000, null)
        };

        var benchmark = new MarketPriceStats(listings).GetBenchmark(listings[0]);

        Assert.Multiple(() =>
        {
            Assert.That(benchmark!.DistrictMedianPerSqm, Is.Null);
            Assert.That(benchmark.CityMedianPerSqm, Is.EqualTo(6_500m));
            Assert.That(benchmark.ReferencePerSqm, Is.EqualTo(6_500m));
        });

        static HouseListing Listing(decimal perSqm, string? location) =>
            new() { Price = perSqm * 50, AreaSqm = 50, Location = location };
    }

    [TestCase("Kraków, Swoszowice", "swoszowice")]
    [TestCase("Kraków,  Stare Miasto ", "stare miasto")]
    [TestCase("Kraków", null)]
    [TestCase("Kraków, ", null)]
    [TestCase(null, null)]
    public void MarketPriceStats_DistrictOf_ParsesLocation(string? location, string? expected)
    {
        Assert.That(MarketPriceStats.DistrictOf(location), Is.EqualTo(expected));
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

        _evaluator.Verify(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.IsAny<HouseListing>(), "Opis z listy", It.IsAny<PriceBenchmark?>(), It.IsAny<CancellationToken>()), Times.Once);
        scraper.Verify(s => s.GetDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RunScan_FetchesDetailsForCandidates_FromMatchingPortal()
    {
        AddUpsert("a", price: 300_000, area: 100, isNew: true);
        AddUpsert("old", price: 300_000, area: 100);
        _upsertResults["old"].Listing.AiScore = 60;

        var scraper = Mock.Get(SucceedingScraper("Otodom", "a", "old"));
        scraper.Setup(s => s.GetDetailsAsync(_upsertResults["a"].Listing.Url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListingDetails("Rok budowy: 2009"));

        var sut = CreateSut([scraper.Object]);

        await sut.RunScanAsync(ProfileId);

        _evaluator.Verify(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.Is<HouseListing>(l => l.ExternalId == "a"),
            "Rok budowy: 2009", It.IsAny<PriceBenchmark?>(), It.IsAny<CancellationToken>()), Times.Once);
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
        _evaluator.Verify(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.IsAny<HouseListing>(), null, It.IsAny<PriceBenchmark?>(), It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.SaveEvaluationAsync(It.IsAny<Guid>(), It.IsAny<ListingEvaluation>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunScan_WhenEvaluationReturnsNullOrThrows_SkipsListingAndContinues()
    {
        AddUpsert("null", price: 100_000, area: 100, isNew: true);
        AddUpsert("throws", price: 200_000, area: 100, isNew: true);
        AddUpsert("ok", price: 300_000, area: 100, isNew: true);

        _evaluator.Setup(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.Is<HouseListing>(l => l.ExternalId == "null"), It.IsAny<string?>(), It.IsAny<PriceBenchmark?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ListingEvaluation?)null);
        _evaluator.Setup(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.Is<HouseListing>(l => l.ExternalId == "throws"), It.IsAny<string?>(), It.IsAny<PriceBenchmark?>(), It.IsAny<CancellationToken>()))
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
        _upsertResults["a"].Listing.AiScore = 60;

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

    [Test]
    public async Task RunScan_AddsDistanceAndRcnValuation_ForApartmentWithCoordinates()
    {
        AddUpsert("a", price: 600_000, area: 50, isNew: true, type: PropertyTypes.Apartment);
        var listingPoint = new GeoPoint(51.1, 17.0);
        var center = new GeoPoint(51.11, 17.03);
        var rcn = new RcnValuation(14_000, 13_800, 30, 0.5, 24);

        var scraper = Mock.Get(SucceedingScraper("Otodom", "a"));
        scraper.Setup(s => s.GetDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListingDetails("Opis", listingPoint));
        var centers = new Mock<ICityCenterProvider>();
        centers.Setup(c => c.GetCenterAsync("Wrocław", It.IsAny<CancellationToken>())).ReturnsAsync(center);
        var valuations = new Mock<IPropertyValuationProvider>();
        valuations.Setup(v => v.GetApartmentValuationAsync(listingPoint, 50, It.IsAny<CancellationToken>())).ReturnsAsync(rcn);

        await CreateSut([scraper.Object], centers: centers.Object, valuations: valuations.Object).RunScanAsync(ProfileId);

        var expectedKm = Math.Round(listingPoint.DistanceKmTo(center), 1);
        _evaluator.Verify(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.IsAny<HouseListing>(), "Opis",
            It.Is<PriceBenchmark?>(b => b != null && b.Rcn == rcn && b.DistanceFromCenterKm == expectedKm && !b.DistanceIsApproximate),
            It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.SaveDistanceAsync(It.IsAny<Guid>(), expectedKm, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunScan_WhenSavingDistanceFails_StillEvaluates()
    {
        AddUpsert("h", price: 900_000, area: 120, isNew: true);
        var scraper = new Mock<IListingScraper>();
        scraper.SetupGet(s => s.PortalName).Returns("Otodom");
        scraper.Setup(s => s.SearchAsync(It.IsAny<SearchProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ScrapeResult.Success(
                [new ScrapedListing { Portal = "Otodom", ExternalId = "h", Url = "https://x/h", Title = "h", Price = 1, AreaSqm = 1, Details = "Opis", Coordinates = new GeoPoint(51.2, 17.1) }]));
        var centers = new Mock<ICityCenterProvider>();
        centers.Setup(c => c.GetCenterAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new GeoPoint(51.11, 17.03));
        _repository.Setup(r => r.SaveDistanceAsync(It.IsAny<Guid>(), It.IsAny<double>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db locked"));

        await CreateSut([scraper.Object], centers: centers.Object).RunScanAsync(ProfileId);

        _evaluator.Verify(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.IsAny<HouseListing>(), It.IsAny<string?>(),
            It.IsAny<PriceBenchmark?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunScan_UsesSearchCoordinates_AndSkipsValuationForHouses()
    {
        AddUpsert("h", price: 900_000, area: 120, isNew: true);
        var point = new GeoPoint(51.2, 17.1, IsApproximate: true);

        var scraper = new Mock<IListingScraper>();
        scraper.SetupGet(s => s.PortalName).Returns("OLX");
        scraper.Setup(s => s.SearchAsync(It.IsAny<SearchProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ScrapeResult.Success(
                [new ScrapedListing { Portal = "Otodom", ExternalId = "h", Url = "https://x/h", Title = "h", Price = 1, AreaSqm = 1, Details = "Opis", Coordinates = point }]));
        var centers = new Mock<ICityCenterProvider>();
        centers.Setup(c => c.GetCenterAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new GeoPoint(51.11, 17.03));
        var valuations = new Mock<IPropertyValuationProvider>();

        await CreateSut([scraper.Object], centers: centers.Object, valuations: valuations.Object).RunScanAsync(ProfileId);

        _evaluator.Verify(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.IsAny<HouseListing>(), "Opis",
            It.Is<PriceBenchmark?>(b => b != null && b.DistanceFromCenterKm > 0 && b.DistanceIsApproximate && b.Rcn == null),
            It.IsAny<CancellationToken>()), Times.Once);
        valuations.Verify(v => v.GetApartmentValuationAsync(It.IsAny<GeoPoint>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
        scraper.Verify(s => s.GetDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RunScan_SavesDistance_ForNonCandidatesWithSearchCoordinates()
    {
        AddUpsert("c", price: 500_000, area: 50, isNew: true);
        AddUpsert("n", price: 900_000, area: 50);
        var center = new GeoPoint(51.11, 17.03);
        var point = new GeoPoint(51.2, 17.1, IsApproximate: true);

        var scraper = new Mock<IListingScraper>();
        scraper.SetupGet(s => s.PortalName).Returns("OLX");
        scraper.Setup(s => s.SearchAsync(It.IsAny<SearchProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ScrapeResult.Success(
            [
                new ScrapedListing { Portal = "Otodom", ExternalId = "c", Url = "https://x/c", Title = "c", Price = 1, AreaSqm = 1, Details = "Opis" },
                new ScrapedListing { Portal = "Otodom", ExternalId = "n", Url = "https://x/n", Title = "n", Price = 1, AreaSqm = 1, Coordinates = point }
            ]));
        var centers = new Mock<ICityCenterProvider>();
        centers.Setup(c => c.GetCenterAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(center);

        await CreateSut([scraper.Object], maxCandidates: 1, centers: centers.Object).RunScanAsync(ProfileId);

        var expectedKm = Math.Round(point.DistanceKmTo(center), 1);
        _repository.Verify(r => r.SaveDistanceAsync(It.IsAny<Guid>(), expectedKm, true, It.IsAny<CancellationToken>()), Times.Once);
        _evaluator.Verify(e => e.EvaluateAsync(It.IsAny<SearchProfile>(), It.Is<HouseListing>(l => l.ExternalId == "c"),
            It.IsAny<string?>(), It.IsAny<PriceBenchmark?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunScan_ApproximateSearchDistance_DoesNotOverwriteExactDistance()
    {
        AddUpsert("c", price: 500_000, area: 50, isNew: true);
        AddUpsert("n", price: 900_000, area: 50);
        _upsertResults["n"].Listing.DistanceFromCenterKm = 3.2;
        _upsertResults["n"].Listing.DistanceIsApproximate = false;

        var scraper = new Mock<IListingScraper>();
        scraper.SetupGet(s => s.PortalName).Returns("OLX");
        scraper.Setup(s => s.SearchAsync(It.IsAny<SearchProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ScrapeResult.Success(
            [
                new ScrapedListing { Portal = "Otodom", ExternalId = "c", Url = "https://x/c", Title = "c", Price = 1, AreaSqm = 1, Details = "Opis" },
                new ScrapedListing { Portal = "Otodom", ExternalId = "n", Url = "https://x/n", Title = "n", Price = 1, AreaSqm = 1, Coordinates = new GeoPoint(51.2, 17.1, IsApproximate: true) }
            ]));
        var centers = new Mock<ICityCenterProvider>();
        centers.Setup(c => c.GetCenterAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new GeoPoint(51.11, 17.03));

        await CreateSut([scraper.Object], maxCandidates: 1, centers: centers.Object).RunScanAsync(ProfileId);

        _repository.Verify(r => r.SaveDistanceAsync(_upsertResults["n"].Listing.Id, It.IsAny<double>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.That(_upsertResults["n"].Listing.DistanceFromCenterKm, Is.EqualTo(3.2));
    }

    [Test]
    public async Task RunScan_BackfillsMissingDistance_FromAdPage_UpToLimit()
    {
        AddUpsert("c", price: 500_000, area: 50, isNew: true);
        AddUpsert("n1", price: 900_000, area: 50);
        AddUpsert("n2", price: 910_000, area: 50);
        AddUpsert("n3", price: 920_000, area: 50);
        AddUpsert("known", price: 930_000, area: 50);
        _upsertResults["known"].Listing.DistanceFromCenterKm = 4.0;
        var center = new GeoPoint(51.11, 17.03);
        var point = new GeoPoint(51.2, 17.1);

        var scraper = Mock.Get(SucceedingScraper("Otodom", "c", "n1", "n2", "n3", "known"));
        scraper.Setup(s => s.GetDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListingDetails("Opis", point));
        var centers = new Mock<ICityCenterProvider>();
        centers.Setup(c => c.GetCenterAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(center);

        await CreateSut([scraper.Object], maxCandidates: 1, centers: centers.Object, maxDistanceBackfill: 2).RunScanAsync(ProfileId);

        var expectedKm = Math.Round(point.DistanceKmTo(center), 1);
        // 1 candidate + 2 backfilled non-candidates; "known" already has a distance.
        scraper.Verify(s => s.GetDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        _repository.Verify(r => r.SaveDistanceAsync(It.IsAny<Guid>(), expectedKm, false, It.IsAny<CancellationToken>()), Times.Exactly(3));
        _repository.Verify(r => r.SaveDistanceAsync(_upsertResults["known"].Listing.Id, It.IsAny<double>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RunScan_DistanceBackfillDisabled_DoesNotFetchNonCandidates()
    {
        AddUpsert("c", price: 500_000, area: 50, isNew: true);
        AddUpsert("n", price: 900_000, area: 50);

        var scraper = Mock.Get(SucceedingScraper("Otodom", "c", "n"));
        scraper.Setup(s => s.GetDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListingDetails("Opis", new GeoPoint(51.2, 17.1)));
        var centers = new Mock<ICityCenterProvider>();
        centers.Setup(c => c.GetCenterAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new GeoPoint(51.11, 17.03));

        await CreateSut([scraper.Object], maxCandidates: 1, centers: centers.Object, maxDistanceBackfill: 0).RunScanAsync(ProfileId);

        scraper.Verify(s => s.GetDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private MarketScanService CreateSut(IEnumerable<IListingScraper> scrapers, int maxCandidates = 8,
        ICityCenterProvider? centers = null, IPropertyValuationProvider? valuations = null, int maxDistanceBackfill = 5) =>
        new(
            scrapers,
            _repository.Object,
            _evaluator.Object,
            _sender.Object,
            Options.Create(new HomeSeekerOptions
            {
                MaxCandidatesPerScan = maxCandidates,
                MaxDistanceBackfillPerScan = maxDistanceBackfill,
                TopListingsInReport = 5,
                RequestDelay = TimeSpan.Zero
            }),
            NullLogger<MarketScanService>.Instance,
            centers,
            valuations);

    private void AddUpsert(string externalId, decimal price, decimal area, bool isNew = false, bool priceDropped = false,
        PropertyTypes type = PropertyTypes.House, string? location = null)
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
                PropertyType = type,
                Location = location
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
