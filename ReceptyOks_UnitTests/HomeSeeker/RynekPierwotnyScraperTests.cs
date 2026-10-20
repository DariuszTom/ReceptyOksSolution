using System.Net;
using System.Text.Json;
using HomeSeeker.Configuration;
using HomeSeeker.Models;
using HomeSeeker.Scrapers;
using HomeSeeker.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ReceptyOks_UnitTests.HomeSeeker;

/// <summary>
/// Tests for <see cref="RynekPierwotnyScraper"/>: URL building with server-side filters,
/// <c>__INITIAL_STATE__</c> parsing (investment → listing with estimated price/area), redirect
/// detection and investment page details. Fixtures are trimmed copies of real pages (2026-10-20).
/// </summary>
public sealed class RynekPierwotnyScraperTests
{
    private const string Offer = """
        {"id":20670,"name":"Lofty Madalińskiego","address":"Kraków, Dębniki, ul. Kobierzyńska 216 A-D",
         "slug":"lofty-madalinskiego-krakow-debniki","type":1,
         "configuration":{"show_price_range":true},
         "construction_date_range":{"lower":"2026-09-30","upper":"2028-06-30"},
         "description":"Jakość z historią w tle.\nKameralna inwestycja.",
         "geo_point":{"type":"Point","coordinates":[19.89563230684685,50.00840135969932]},
         "region":{"short_name":"Kraków, Dębniki","city":"Kraków","district":"Dębniki"},
         "vendor":{"name":"Convector Development Spółka z o.o.","slug":"convector-development-spolka-z-oo"},
         "stats":{"distance_from_region":{"distance":7.2},"properties_count_for_sale":99,
                  "ranges_area_max":115,"ranges_area_min":30,"ranges_price_m2_max":19300,"ranges_price_m2_min":16000,
                  "ranges_price_max":2051450,"ranges_price_min":537562,"ranges_rooms_max":6,"ranges_rooms_min":1}}
        """;

    private const string HiddenPriceOffer = """
        {"id":111,"name":"Ukryte ceny","slug":"ukryte","configuration":{"show_price_range":false},
         "vendor":{"name":"Dev","slug":"dev"},
         "stats":{"ranges_area_min":40,"ranges_area_max":80,"ranges_price_min":0,"ranges_price_max":0}}
        """;

    private static string Page(string offers, string query = """{"region":["11158"],"region_name":"krakow","type":"1"}""") => $$$"""
        <html><body><div id="root"></div>
        <script>
            window.__INITIAL_STATE__ = {"abTesting":{},"offerList":{"list":{"count":145,"page":1,"page_size":24,
              "latestQuery":{{{query}}},"offers":[{{{offers}}}] } } };
            window.__OTHER__ = {"x":1};
        </script></body></html>
        """;

    private static readonly string SearchPage = Page($"{Offer},{HiddenPriceOffer}");
    private static readonly string EmptyPage = Page(string.Empty);

    private static readonly string DetailPage = $$$"""
        <html><script>window.__INITIAL_STATE__ = {"offerDetail":{"offer":{{{Offer}}} } };</script></html>
        """;

    private static readonly SearchProfile Profile = new()
    {
        Id = Guid.NewGuid(),
        City = "Kraków",
        PropertyTypes = PropertyTypes.Apartment,
        MinPrice = 600_000,
        MaxPrice = 900_000,
        MinAreaSqm = 40,
        MinRooms = 3,
        NotificationEmail = "a@example.com"
    };

    [Test]
    public void ParseSearchResults_MapsInvestmentAndSkipsHiddenPrices()
    {
        var result = RynekPierwotnyScraper.ParseSearchResults(SearchPage, profile: null, expectedPage: 1);

        Assert.That(result.Problem, Is.Null);
        Assert.That(result.Listings, Has.Count.EqualTo(1));
        var listing = result.Listings[0];
        Assert.Multiple(() =>
        {
            Assert.That(listing.Portal, Is.EqualTo(PortalNames.RynekPierwotny));
            Assert.That(listing.ExternalId, Is.EqualTo("20670"));
            Assert.That(listing.Url, Is.EqualTo(
                "https://rynekpierwotny.pl/oferty/convector-development-spolka-z-oo/lofty-madalinskiego-krakow-debniki-20670/"));
            Assert.That(listing.Title, Is.EqualTo("Lofty Madalińskiego – Convector Development Spółka z o.o."));
            Assert.That(listing.Location, Is.EqualTo("Kraków, Dębniki"));
            Assert.That(listing.DevelopmentKey, Is.EqualTo("rynekpierwotny:20670"));
            Assert.That(listing.AreaSqm, Is.EqualTo(30m));
            Assert.That(listing.Price, Is.EqualTo(537_562m));
            Assert.That(listing.Rooms, Is.EqualTo(1));
            Assert.That(listing.Coordinates, Is.Not.Null);
            Assert.That(listing.Coordinates!.Value.Latitude, Is.EqualTo(50.0084013).Within(1e-6));
            Assert.That(listing.Coordinates.Value.Longitude, Is.EqualTo(19.8956323).Within(1e-6));
            Assert.That(listing.Coordinates.Value.IsApproximate, Is.False);
            Assert.That(listing.Details, Does.Contain("Rynek: pierwotny"));
            Assert.That(listing.Details, Does.Contain("Convector Development"));
            Assert.That(listing.Details, Does.Contain("Termin oddania: 2026-09-30 – 2028-06-30"));
            Assert.That(listing.Details, Does.Contain("Kameralna inwestycja"));
        });
    }

    [Test]
    public void ParseSearchResults_FitsEstimateToProfileSoItPassesTheFilter()
    {
        var listing = RynekPierwotnyScraper.ParseSearchResults(SearchPage, Profile, expectedPage: 1).Listings.Single();

        Assert.Multiple(() =>
        {
            // 3 of 1–6 rooms → 30 + 85 × 2/6 ≈ 58 m²; 58 × 16 000 = 928 000 zł, clamped to the profile's 900 000 zł.
            Assert.That(listing.AreaSqm, Is.EqualTo(58m));
            Assert.That(listing.Price, Is.EqualTo(900_000m));
            Assert.That(listing.Rooms, Is.EqualTo(3));
            Assert.That(ListingSelection.MatchesCriteria(Profile, listing), Is.True);
        });
    }

    [Test]
    public void EstimateUnit_RaisesPriceToProfileMinimum()
    {
        var ranges = new RynekPierwotnyScraper.Ranges(500_000, 1_000_000, 10_000, 20_000, 30, 100, 1, 4);
        var profile = new SearchProfile { MinPrice = 700_000, MinAreaSqm = 35 };

        var (price, area, _) = RynekPierwotnyScraper.EstimateUnit(ranges, profile);

        Assert.That(area, Is.EqualTo(35m));
        Assert.That(price, Is.EqualTo(700_000m));
    }

    [Test]
    public void EstimateUnit_ProfileNeedsMoreRooms_ScalesAreaWithinInvestmentRange()
    {
        var ranges = new RynekPierwotnyScraper.Ranges(500_000, 1_500_000, 12_000, 15_000, 40, 100, 2, 4);
        var profile = new SearchProfile { MinAreaSqm = 70, MaxAreaSqm = 110, MinRooms = 4, MaxPrice = 1_400_000 };

        var (price, area, rooms) = RynekPierwotnyScraper.EstimateUnit(ranges, profile);

        Assert.Multiple(() =>
        {
            // Rooms 2–4 split 40–100 m² into thirds; 4 rooms start at 40 + 60 × 2/3 = 80 m².
            Assert.That(area, Is.EqualTo(80m));
            Assert.That(price, Is.EqualTo(960_000m));
            Assert.That(rooms, Is.EqualTo(4));
        });
    }

    [Test]
    public void EstimateUnit_RoomEstimateBelowProfileMinimum_UsesProfileMinimumArea()
    {
        var ranges = new RynekPierwotnyScraper.Ranges(400_000, 1_500_000, 12_000, 15_000, 30, 90, 1, 4);
        var profile = new SearchProfile { MinAreaSqm = 70, MinRooms = 2 };

        var (_, area, rooms) = RynekPierwotnyScraper.EstimateUnit(ranges, profile);

        Assert.That(area, Is.EqualTo(70m));
        Assert.That(rooms, Is.EqualTo(2));
    }

    [Test]
    public void EstimateUnit_RoomsAboveInvestmentRange_KeepsSmallestArea()
    {
        var ranges = new RynekPierwotnyScraper.Ranges(400_000, 900_000, 12_000, 15_000, 30, 60, 1, 3);
        var profile = new SearchProfile { MinRooms = 4 };

        var (_, area, rooms) = RynekPierwotnyScraper.EstimateUnit(ranges, profile);

        Assert.That(area, Is.EqualTo(30m));
        Assert.That(rooms, Is.EqualTo(1));
    }

    [Test]
    public void EstimateUnit_WithoutOverlap_KeepsInvestmentValuesSoFilterRejectsIt()
    {
        var ranges = new RynekPierwotnyScraper.Ranges(1_200_000, 2_000_000, 20_000, 25_000, 60, 120, 2, 5);
        var profile = new SearchProfile { MaxPrice = 900_000, MaxAreaSqm = 50 };

        var (price, area, _) = RynekPierwotnyScraper.EstimateUnit(ranges, profile);

        Assert.That(area, Is.EqualTo(60m));
        Assert.That(price, Is.EqualTo(1_200_000m));
    }

    [Test]
    public void ParseSearchResults_UnknownCityRedirect_ReturnsNothing()
    {
        var html = Page(Offer, query: "{}");

        var result = RynekPierwotnyScraper.ParseSearchResults(html, Profile, expectedPage: 1);

        Assert.That(result.Listings, Is.Empty);
        Assert.That(result.Problem, Does.Contain("nieznane miasto"));
    }

    [Test]
    public void ParseSearchResults_PagePastEndRedirectedToFirst_ReturnsNothing()
    {
        var result = RynekPierwotnyScraper.ParseSearchResults(SearchPage, Profile, expectedPage: 3);

        Assert.That(result.Listings, Is.Empty);
        Assert.That(result.Problem, Is.Null);
    }

    [Test]
    public void ParseSearchResults_MatchingPage_IsAccepted()
    {
        var html = Page(Offer, query: """{"region":["11158"],"page":"2","type":"1"}""");

        Assert.That(RynekPierwotnyScraper.ParseSearchResults(html, Profile, expectedPage: 2).Listings, Has.Count.EqualTo(1));
    }

    [TestCase("<html></html>")]
    [TestCase("<script>window.__INITIAL_STATE__ = {broken</script>")]
    public void ParseSearchResults_WithoutState_ReturnsNothing(string html)
    {
        Assert.That(RynekPierwotnyScraper.ParseSearchResults(html, Profile, 1).Listings, Is.Empty);
    }

    [Test]
    public void ReadInitialState_StopsAtEndOfObject()
    {
        using var state = RynekPierwotnyScraper.ReadInitialState(SearchPage);

        Assert.That(state, Is.Not.Null);
        Assert.That(state!.RootElement.TryGetProperty("offerList", out _), Is.True);
    }

    [Test]
    public void ReadCoordinates_InvalidPoint_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("""{"geo_point":{"coordinates":[0,0]}}""");

        Assert.That(RynekPierwotnyScraper.ReadCoordinates(doc.RootElement), Is.Null);
    }

    [TestCase("https://rynekpierwotny.pl/oferty/dev/x-1/", true)]
    [TestCase("http://rynekpierwotny.pl/oferty/dev/x-1/", false)]
    [TestCase("https://rynekpierwotny.pl.evil.com/oferty/", false)]
    public void IsRynekPierwotnyUrl_AcceptsHttpsDomainOnly(string url, bool expected)
    {
        Assert.That(RynekPierwotnyScraper.IsRynekPierwotnyUrl(url), Is.EqualTo(expected));
    }

    [Test]
    public async Task Search_BuildsCityUrlWithoutFiltersAndPagesUntilEmpty()
    {
        var handler = new StubHandler((HttpStatusCode.OK, SearchPage), (HttpStatusCode.OK, EmptyPage));

        var result = await Create(handler, maxPages: 5).SearchAsync(Profile);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Listings, Has.Count.EqualTo(1));
            Assert.That(result.Listings[0].PropertyType, Is.EqualTo(PropertyTypes.Apartment));
            Assert.That(handler.Requests, Has.Count.EqualTo(2));
            Assert.That(handler.Requests[0].AbsoluteUri, Is.EqualTo(
                "https://rynekpierwotny.pl/s/nowe-mieszkania-krakow/"));
            Assert.That(handler.Requests[1].Query, Is.EqualTo("?page=2"));
        });
    }

    [Test]
    public async Task Search_Houses_UsesNormalizedCitySlug()
    {
        var handler = new StubHandler((HttpStatusCode.OK, EmptyPage));
        var profile = new SearchProfile
        {
            Id = Guid.NewGuid(),
            City = "Bielsko-Biała",
            PropertyTypes = PropertyTypes.House,
            NotificationEmail = "a@example.com"
        };

        await Create(handler).SearchAsync(profile);

        Assert.That(handler.Requests[0].AbsoluteUri, Is.EqualTo("https://rynekpierwotny.pl/s/nowe-domy-bielsko-biala/"));
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
        var handler = new StubHandler((HttpStatusCode.OK, DetailPage));

        var details = await Create(handler).GetDetailsAsync("https://example.com/oferty/x/");

        Assert.That(details, Is.Null);
        Assert.That(handler.Requests, Is.Empty);
    }

    [Test]
    public async Task GetDetails_ReturnsTextAndCoordinates()
    {
        var details = await Create(new StubHandler((HttpStatusCode.OK, DetailPage)))
            .GetDetailsAsync("https://rynekpierwotny.pl/oferty/convector-development-spolka-z-oo/lofty-madalinskiego-krakow-debniki-20670/");

        Assert.That(details, Is.Not.Null);
        Assert.That(details!.Text, Does.Contain("Ceny lokali"));
        Assert.That(details.Coordinates, Is.Not.Null);
    }

    [Test]
    public void MarketPriceStats_IgnoresPrimaryMarketInvestments()
    {
        var listings = new[]
        {
            new HouseListing { Portal = PortalNames.Otodom, PropertyType = PropertyTypes.Apartment, Price = 500_000, AreaSqm = 50, Location = "Kraków" },
            new HouseListing { Portal = PortalNames.RynekPierwotny, PropertyType = PropertyTypes.Apartment, Price = 300_000, AreaSqm = 100, Location = "Kraków" }
        };

        var benchmark = new MarketPriceStats(listings).GetBenchmark(listings[1]);

        Assert.That(benchmark, Is.Not.Null);
        Assert.That(benchmark!.CitySampleSize, Is.EqualTo(1));
        Assert.That(benchmark.CityMedianPerSqm, Is.EqualTo(10_000m));
    }

    private static RynekPierwotnyScraper Create(StubHandler handler, int maxPages = 1)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(ListingScraperBase.HttpClientName))
            .Returns(() => new HttpClient(handler, disposeHandler: false));
        var options = Microsoft.Extensions.Options.Options.Create(new HomeSeekerOptions
        {
            MaxSearchPagesPerPortal = maxPages,
            RequestDelay = TimeSpan.Zero
        });
        return new RynekPierwotnyScraper(factory.Object, options, NullLogger<RynekPierwotnyScraper>.Instance);
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
