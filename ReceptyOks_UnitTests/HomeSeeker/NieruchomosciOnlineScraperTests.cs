using System.Net;
using HomeSeeker.Configuration;
using HomeSeeker.Models;
using HomeSeeker.Scrapers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ReceptyOks_UnitTests.HomeSeeker;

/// <summary>
/// Tests for <see cref="NieruchomosciOnlineScraper"/>: URL building, search page parsing
/// (cached props JSON joined with HTML tiles, tile fallback) and ad page details/coordinates.
/// Fixtures are trimmed copies of real pages (2026-10-18).
/// </summary>
public sealed class NieruchomosciOnlineScraperTests
{
    private const string SearchPage = """
        <html><body>
        <div class="tile tile-tile" data-id="a26441071" data-market-type="secondary">
          <h2 class=" name body-lg"><a href="https://krakow.nieruchomosci-online.pl/mieszkanie,z-balkonem/26441071.html?3,mieszkanie,sprzedaz,,Kraków,,,,500000-800000,40-70">Mieszkanie 3-pokojowe, ul. Zabawa</a></h2>
          <p class="province body-md"><a class="margin-right4" href="//krakow.nieruchomosci-online.pl/mieszkania,nowy-swiat/">Nowy Świat,</a><span class="margin-right4">Kraków</span></p>
          <p class="title-a primary-display"><span>672&nbsp;258&nbsp;zł</span><span class="area">61,17&nbsp;m&sup2;</span></p>
        </div>
        <div class="tile tile-tile tile-investment-strip" data-id="i46606_26733533" data-market-type="primary">
          <h2 class=" name body-lg"><a href="https://krakow.nieruchomosci-online.pl/nowe-mieszkanie,dzialowskiego-zacisze/26733533.html?3,mieszkanie">Mieszkanie 3-pokojowe, ul. Działowskiego 52</a></h2>
          <p class="province body-md">Kliny Zacisze Kraków</p>
          <p class="title-a primary-display"><span>702&nbsp;600&nbsp;zł</span><span class="area">69,02&nbsp;m&sup2;</span></p>
        </div>
        <div class="tile tile-tile" data-id="a99" data-market-type="secondary">
          <h2 class="name"><a href="https://krakow.nieruchomosci-online.pl/mieszkanie,wynajem/99.html">Na wynajem</a></h2>
          <p class="primary-display"><span>3&nbsp;000&nbsp;zł</span><span class="area">40&nbsp;m&sup2;</span></p>
        </div>
        <div class="tile tile-tile" data-id="a100">
          <h2 class="name"><a href="https://evil.example.com/100.html">Obca domena</a></h2>
        </div>
        <script>
        NOTrafficEventBuilder.buildCachedProps({"list_props":{"pgpg":1,"pgpgs":53,"tiles":2070},"record_props":{
          "a26441071":{"rccata":"flats","rctrn":"sale","rcmrkt":"secondary","rlocqtn":"Nowy Świat","rlocstsn":"Zabawa","prc":672258,"rsur":61.17,"rooms":3},
          "i46606_26733533":{"rccata":"flats","rctrn":"sale","rcmrkt":"primary","rlocqtn":"Kliny Zacisze","rlocstsn":"Działowskiego 52","prc":702600,"rsur":69.02,"rooms":3},
          "a99":{"rctrn":"rent","prc":3000,"rsur":40,"rooms":2},
          "a100":{"rctrn":"sale","prc":500000,"rsur":50,"rooms":2}
        }});
        </script>
        </body></html>
        """;

    private const string EmptySearchPage = """
        <script>NOTrafficEventBuilder.buildCachedProps({"list_props":{"pgpg":1,"pgpgs":1},"record_props":[]});</script>
        """;

    private const string AdPage = """
        <html><head>
        <script type="application/ld+json">{"@context":"https://schema.org","@type":"WebPage","name":"Strona"}</script>
        <script type="application/ld+json">{"@context":"https://schema.org","@type":"Apartment","description":"Krótki opis.",
          "amenityFeature":[{"name":"Balcony","value":true},{"name":"Elevator","value":false}],
          "additionalProperty":[{"name":"Number of rooms","value":"3"},{"name":"Floor level","value":"1"},{"name":"Number of bedrooms","value":""}],
          "geo":{"@type":"GeoCoordinates","latitude":"50.0223613","longitude":"19.9619316"}}</script>
        </head><body>
        <div class="box-offer-custom-desc" id="boxCustomDesc">
          <div class="estate-desc-less"><p>Skrót <span id="readMore">(...)</span></p></div>
          <div class="estate-desc-more" style="display: none;"><p>Pełny opis z&nbsp;balkonem.<br />Druga linia.</p></div>
        </div>
        </body></html>
        """;

    private static readonly SearchProfile Profile = new()
    {
        Id = Guid.NewGuid(),
        City = "Kraków",
        MinPrice = 500_000,
        MaxPrice = 800_000,
        MinAreaSqm = 40,
        NotificationEmail = "a@example.com"
    };

    [Test]
    public void ParseSearchResults_JoinsCachedPropsWithTiles()
    {
        var listings = NieruchomosciOnlineScraper.ParseSearchResults(SearchPage);

        Assert.That(listings, Has.Count.EqualTo(2), "rent and foreign-domain tiles are skipped");
        var regular = listings[0];
        Assert.Multiple(() =>
        {
            Assert.That(regular.Portal, Is.EqualTo(PortalNames.NieruchomosciOnline));
            Assert.That(regular.ExternalId, Is.EqualTo("26441071"));
            Assert.That(regular.Url, Is.EqualTo("https://krakow.nieruchomosci-online.pl/mieszkanie,z-balkonem/26441071.html"));
            Assert.That(regular.Title, Is.EqualTo("Mieszkanie 3-pokojowe, ul. Zabawa"));
            Assert.That(regular.Price, Is.EqualTo(672258m));
            Assert.That(regular.AreaSqm, Is.EqualTo(61.17m));
            Assert.That(regular.Rooms, Is.EqualTo(3));
            Assert.That(regular.Location, Is.EqualTo("Nowy Świat, Kraków, Zabawa"));
            Assert.That(regular.DevelopmentKey, Is.Null);
            Assert.That(regular.Details, Is.Null);
        });

        var unit = listings[1];
        Assert.Multiple(() =>
        {
            Assert.That(unit.ExternalId, Is.EqualTo("26733533"));
            Assert.That(unit.DevelopmentKey, Is.EqualTo("nieruchomosci-online:46606"));
            Assert.That(unit.Price, Is.EqualTo(702600m));
            Assert.That(unit.Details, Does.Contain("pierwotny"));
        });
    }

    [Test]
    public void ParseSearchResults_WithoutCachedProps_FallsBackToTiles()
    {
        var html = SearchPage[..SearchPage.IndexOf("<script>", StringComparison.Ordinal)];

        var listings = NieruchomosciOnlineScraper.ParseSearchResults(html);

        Assert.That(listings.Select(l => l.ExternalId), Is.EqualTo(new[] { "26441071", "26733533", "99" }));
        Assert.Multiple(() =>
        {
            Assert.That(listings[0].Price, Is.EqualTo(672258m));
            Assert.That(listings[0].AreaSqm, Is.EqualTo(61.17m));
            Assert.That(listings[1].DevelopmentKey, Is.EqualTo("nieruchomosci-online:46606"));
        });
    }

    [Test]
    public void ParseSearchResults_PastLastPage_ReturnsEmpty()
    {
        Assert.That(NieruchomosciOnlineScraper.ParseSearchResults(EmptySearchPage), Is.Empty);
    }

    [TestCase("a123", "123", null)]
    [TestCase("i456_123", "123", "nieruchomosci-online:456")]
    [TestCase("x1", null, null)]
    public void ParseKey_MapsTileKeys(string key, string? id, string? development)
    {
        Assert.That(NieruchomosciOnlineScraper.ParseKey(key), Is.EqualTo((id, development)));
    }

    [Test]
    public void ParseDetails_ReadsParametersAndFullDescription()
    {
        var details = NieruchomosciOnlineScraper.ParseDetails(AdPage);

        Assert.That(details, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(details, Does.Contain("- Number of rooms: 3"));
            Assert.That(details, Does.Contain("- Floor level: 1"));
            Assert.That(details, Does.Not.Contain("Number of bedrooms"));
            Assert.That(details, Does.Contain("Balcony").And.Not.Contain("Elevator"));
            Assert.That(details, Does.Contain("Pełny opis z balkonem."));
            Assert.That(details, Does.Not.Contain("Skrót"));
        });
    }

    [Test]
    public void ParseCoordinates_ReadsJsonLdGeo()
    {
        var point = NieruchomosciOnlineScraper.ParseCoordinates(AdPage);

        Assert.That(point, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(point!.Value.Latitude, Is.EqualTo(50.0223613).Within(1e-7));
            Assert.That(point.Value.Longitude, Is.EqualTo(19.9619316).Within(1e-7));
            Assert.That(point.Value.IsApproximate, Is.False);
        });
    }

    [Test]
    public void ParseCoordinates_WithoutGeo_ReturnsNull()
    {
        Assert.That(NieruchomosciOnlineScraper.ParseCoordinates("<html></html>"), Is.Null);
    }

    [TestCase("https://krakow.nieruchomosci-online.pl/mieszkanie,x/1.html", true)]
    [TestCase("https://www.nieruchomosci-online.pl/szukaj.html", true)]
    [TestCase("http://krakow.nieruchomosci-online.pl/1.html", false)]
    [TestCase("https://nieruchomosci-online.pl.evil.com/1.html", false)]
    public void IsNieruchomosciOnlineUrl_AcceptsHttpsSubdomainsOnly(string url, bool expected)
    {
        Assert.That(NieruchomosciOnlineScraper.IsNieruchomosciOnlineUrl(url), Is.EqualTo(expected));
    }

    [Test]
    public async Task Search_BuildsFilteredUrlAndPagesUntilEmpty()
    {
        var handler = new StubHandler(
            (HttpStatusCode.OK, SearchPage),
            (HttpStatusCode.OK, EmptySearchPage));
        var sut = Create(handler, maxPages: 5);

        var result = await sut.SearchAsync(Profile);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Listings, Has.Count.EqualTo(2));
            Assert.That(handler.Requests, Has.Count.EqualTo(2));
            Assert.That(handler.Requests[0].AbsoluteUri, Is.EqualTo(
                "https://www.nieruchomosci-online.pl/szukaj.html?3,dom,sprzedaz,,Krak%C3%B3w,,,,500000-800000,40-"));
            Assert.That(handler.Requests[1].Query, Does.EndWith("&p=2"));
        });
    }

    [Test]
    public async Task Search_ApartmentsWithoutFilters_UsesEmptyRanges()
    {
        var handler = new StubHandler((HttpStatusCode.OK, EmptySearchPage));
        var profile = new SearchProfile
        {
            Id = Guid.NewGuid(),
            City = "Wrocław",
            PropertyTypes = PropertyTypes.Apartment,
            NotificationEmail = "a@example.com"
        };

        await Create(handler).SearchAsync(profile);

        Assert.That(handler.Requests[0].AbsoluteUri, Is.EqualTo(
            "https://www.nieruchomosci-online.pl/szukaj.html?3,mieszkanie,sprzedaz,,Wroc%C5%82aw,,,,,"));
    }

    [Test]
    public async Task Search_On403_FailsPortal()
    {
        var result = await Create(new StubHandler((HttpStatusCode.Forbidden, "blocked"))).SearchAsync(Profile);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Does.Contain("403"));
    }

    [Test]
    public async Task GetDetails_RejectsForeignDomain()
    {
        var handler = new StubHandler((HttpStatusCode.OK, AdPage));

        var details = await Create(handler).GetDetailsAsync("https://example.com/1.html");

        Assert.That(details, Is.Null);
        Assert.That(handler.Requests, Is.Empty);
    }

    [Test]
    public async Task GetDetails_ReturnsTextAndCoordinates()
    {
        var details = await Create(new StubHandler((HttpStatusCode.OK, AdPage)))
            .GetDetailsAsync("https://krakow.nieruchomosci-online.pl/mieszkanie,x/27030818.html");

        Assert.That(details, Is.Not.Null);
        Assert.That(details!.Text, Does.Contain("Pełny opis"));
        Assert.That(details.Coordinates, Is.Not.Null);
    }

    private static NieruchomosciOnlineScraper Create(StubHandler handler, int maxPages = 1)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(ListingScraperBase.HttpClientName))
            .Returns(() => new HttpClient(handler, disposeHandler: false));
        var options = Microsoft.Extensions.Options.Options.Create(new HomeSeekerOptions
        {
            MaxSearchPagesPerPortal = maxPages,
            RequestDelay = TimeSpan.Zero
        });
        return new NieruchomosciOnlineScraper(factory.Object, options, NullLogger<NieruchomosciOnlineScraper>.Instance);
    }

    private sealed class StubHandler(params (HttpStatusCode Status, string Body)[] responses) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            var (status, body) = responses[Math.Min(Requests.Count - 1, responses.Length - 1)];
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}
