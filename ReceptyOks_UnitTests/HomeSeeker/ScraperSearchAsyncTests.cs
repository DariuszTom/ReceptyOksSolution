using System.Net;
using HomeSeeker.Abstractions;
using HomeSeeker.Configuration;
using HomeSeeker.Models;
using HomeSeeker.Scrapers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ReceptyOks_UnitTests.HomeSeeker;

/// <summary>
/// Tests the HTTP paging/failure contract of the scrapers' <c>SearchAsync</c>
/// with a stubbed <see cref="HttpMessageHandler"/>.
/// </summary>
public sealed class ScraperSearchAsyncTests
{
    private const string OtodomPage = """
        <script id="__NEXT_DATA__" type="application/json">
        {"props":{"pageProps":{"data":{"searchAds":{"items":[
          {"id":1,"slug":"dom-1","title":"Dom 1","totalPrice":{"value":500000},"areaInSquareMeters":100},
          {"id":2,"slug":"dom-2","title":"Dom 2","totalPrice":{"value":700000},"areaInSquareMeters":140}
        ]}}}}}
        </script>
        """;

    private const string EmptyPage = "<html><body></body></html>";

    private const string OlxPage = """
        <script>
        window.__PRERENDERED_STATE__= "{\"listing\":{\"listing\":{\"ads\":[{\"id\":123,\"url\":\"https://www.olx.pl/d/oferta/x-ID123.html\",\"title\":\"Oferta\",\"price\":{\"regularPrice\":{\"value\":650000}},\"params\":[{\"key\":\"m\",\"value\":{\"key\":\"62\"}}],\"location\":{\"city\":{\"name\":\"Wroclaw\"}}}]}}}";
        </script>
        """;

    private static readonly SearchProfile Profile = new()
    {
        Id = Guid.NewGuid(),
        City = "Wrocław Śródmieście",
        MinPrice = 400_000,
        MaxPrice = 900_000,
        MinAreaSqm = 90,
        NotificationEmail = "a@example.com"
    };

    [Test]
    public async Task Otodom_WithListingsThenEmptyPage_ReturnsSuccessAndStopsPaging()
    {
        var handler = new StubHandler(
            (HttpStatusCode.OK, OtodomPage),
            (HttpStatusCode.OK, EmptyPage));
        var sut = CreateOtodom(handler, maxPages: 5);

        var result = await sut.SearchAsync(Profile);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Listings.Select(l => l.ExternalId), Is.EqualTo(new[] { "1", "2" }));
        Assert.That(handler.Requests, Has.Count.EqualTo(2), "paging must stop on the first empty page");
    }

    [Test]
    public async Task Otodom_BuildsUrlWithNormalizedCityAndFilters()
    {
        var handler = new StubHandler((HttpStatusCode.OK, EmptyPage));
        var sut = CreateOtodom(handler);

        await sut.SearchAsync(Profile);

        var url = handler.Requests[0].ToString();
        Assert.Multiple(() =>
        {
            Assert.That(url, Does.StartWith("https://www.otodom.pl/pl/oferty/sprzedaz/dom/wroclaw-srodmiescie?page=1"));
            Assert.That(url, Does.Contain("priceMin=400000"));
            Assert.That(url, Does.Contain("priceMax=900000"));
            Assert.That(url, Does.Contain("areaMin=90"));
            Assert.That(url, Does.Not.Contain("areaMax"));
        });
    }

    [Test]
    public async Task Otodom_WithBothTypes_QueriesHousesThenApartmentsAndTagsListings()
    {
        var handler = new StubHandler(
            (HttpStatusCode.OK, OtodomPage),
            (HttpStatusCode.OK, OtodomPage));
        var sut = CreateOtodom(handler, maxPages: 1);

        var result = await sut.SearchAsync(ProfileOf(PropertyTypes.All));

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests, Has.Count.EqualTo(2));
            Assert.That(handler.Requests[0].AbsolutePath, Does.Contain("/sprzedaz/dom/"));
            Assert.That(handler.Requests[1].AbsolutePath, Does.Contain("/sprzedaz/mieszkanie/"));
            Assert.That(result.Listings.Select(l => l.PropertyType),
                Is.EqualTo(new[] { PropertyTypes.House, PropertyTypes.House, PropertyTypes.Apartment, PropertyTypes.Apartment }));
        });
    }

    [Test]
    public async Task Otodom_With403OnHouses_DoesNotQueryApartments()
    {
        var handler = new StubHandler((HttpStatusCode.Forbidden, "blocked"));
        var sut = CreateOtodom(handler);

        var result = await sut.SearchAsync(ProfileOf(PropertyTypes.All));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(handler.Requests, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Otodom_WhenApartmentsFailAfterHouses_ReturnsSuccessWithWarning()
    {
        var handler = new StubHandler(
            (HttpStatusCode.OK, OtodomPage),
            (HttpStatusCode.InternalServerError, "oops"));
        var sut = CreateOtodom(handler, maxPages: 1);

        var result = await sut.SearchAsync(ProfileOf(PropertyTypes.All));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Listings, Has.Count.EqualTo(2));
            Assert.That(result.Warnings, Is.EqualTo(new[] { "mieszkanie - nie pobrano" }));
        });
    }

    [Test]
    public async Task Otodom_WithBothTypesSucceeding_HasNoWarnings()
    {
        var sut = CreateOtodom(new StubHandler((HttpStatusCode.OK, OtodomPage)), maxPages: 1);

        var result = await sut.SearchAsync(ProfileOf(PropertyTypes.All));

        Assert.That(result.Warnings, Is.Empty);
    }

    [Test]
    public async Task Olx_WithBothTypes_QueriesHousesThenApartmentsAndTagsListings()
    {
        var handler = new StubHandler(
            (HttpStatusCode.OK, OlxPage),
            (HttpStatusCode.OK, OlxPage));
        var sut = CreateOlx(handler, maxPages: 1);

        var result = await sut.SearchAsync(ProfileOf(PropertyTypes.All));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(handler.Requests, Has.Count.EqualTo(2));
            Assert.That(handler.Requests[0].AbsolutePath, Does.StartWith("/nieruchomosci/domy/sprzedaz/"));
            Assert.That(handler.Requests[1].AbsolutePath, Does.StartWith("/nieruchomosci/mieszkania/sprzedaz/"));
            Assert.That(result.Listings.Select(l => l.PropertyType),
                Is.EqualTo(new[] { PropertyTypes.House, PropertyTypes.Apartment }));
            Assert.That(result.Warnings, Is.Empty);
        });
    }

    [Test]
    public async Task Olx_With403OnHouses_SkipsApartmentsAndFails()
    {
        var handler = new StubHandler((HttpStatusCode.Forbidden, "blocked"));
        var sut = CreateOlx(handler);

        var result = await sut.SearchAsync(ProfileOf(PropertyTypes.All));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(handler.Requests, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Olx_WithApartmentsOnly_UsesApartmentCategory()
    {
        var handler = new StubHandler((HttpStatusCode.OK, EmptyPage));
        var sut = CreateOlx(handler);

        await sut.SearchAsync(ProfileOf(PropertyTypes.Apartment));

        Assert.That(handler.Requests, Has.Count.EqualTo(1));
        Assert.That(handler.Requests[0].AbsolutePath, Does.StartWith("/nieruchomosci/mieszkania/sprzedaz/"));
    }

    [Test]
    public async Task Otodom_RespectsMaxSearchPagesPerPortal()
    {
        var handler = new StubHandler(
            (HttpStatusCode.OK, OtodomPage),
            (HttpStatusCode.OK, OtodomPage),
            (HttpStatusCode.OK, OtodomPage));
        var sut = CreateOtodom(handler, maxPages: 2);

        var result = await sut.SearchAsync(Profile);

        Assert.That(handler.Requests, Has.Count.EqualTo(2));
        Assert.That(result.Listings, Has.Count.EqualTo(4));
    }

    [Test]
    public async Task Otodom_With403OnFirstPage_ReturnsFailure()
    {
        var sut = CreateOtodom(new StubHandler((HttpStatusCode.Forbidden, "blocked")));

        var result = await sut.SearchAsync(Profile);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Does.Contain("403"));
        Assert.That(result.Listings, Is.Empty);
    }

    [Test]
    public async Task Otodom_WithServerErrorOnFirstPage_ReturnsFailure()
    {
        var sut = CreateOtodom(new StubHandler((HttpStatusCode.InternalServerError, "oops")));

        var result = await sut.SearchAsync(Profile);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Does.Contain("Otodom request failed"));
    }

    [Test]
    public async Task Otodom_WithErrorAfterFirstPage_ReturnsPartialSuccess()
    {
        var sut = CreateOtodom(new StubHandler(
            (HttpStatusCode.OK, OtodomPage),
            (HttpStatusCode.Forbidden, "blocked")), maxPages: 3);

        var result = await sut.SearchAsync(Profile);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Listings, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Otodom_WithUnparseablePage_ReturnsEmptySuccess()
    {
        var sut = CreateOtodom(new StubHandler((HttpStatusCode.OK, "<html>captcha</html>")));

        var result = await sut.SearchAsync(Profile);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Listings, Is.Empty);
    }

    [Test]
    public async Task Otodom_WhenCancelledBeforeStart_DoesNotThrow()
    {
        var handler = new StubHandler((HttpStatusCode.OK, OtodomPage));
        var sut = CreateOtodom(handler);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await sut.SearchAsync(Profile, cts.Token);

        Assert.That(handler.Requests, Is.Empty);
        Assert.That(result.Listings, Is.Empty);
    }

    [Test]
    public async Task Olx_With403OnFirstPage_ReturnsFailure()
    {
        var sut = CreateOlx(new StubHandler((HttpStatusCode.Forbidden, "blocked")));

        var result = await sut.SearchAsync(Profile);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Does.Contain("403"));
    }

    [Test]
    public async Task Olx_WithEmptyPage_ReturnsEmptySuccessAfterOneRequest()
    {
        var handler = new StubHandler((HttpStatusCode.OK, EmptyPage));
        var sut = CreateOlx(handler, maxPages: 3);

        var result = await sut.SearchAsync(Profile);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Listings, Is.Empty);
        Assert.That(handler.Requests, Has.Count.EqualTo(1));
        Assert.That(handler.Requests[0].Host, Is.EqualTo("www.olx.pl"));
    }

    private static SearchProfile ProfileOf(PropertyTypes types) => new()
    {
        Id = Profile.Id,
        City = Profile.City,
        MinPrice = Profile.MinPrice,
        MaxPrice = Profile.MaxPrice,
        MinAreaSqm = Profile.MinAreaSqm,
        NotificationEmail = Profile.NotificationEmail,
        PropertyTypes = types
    };

    private static OtodomScraper CreateOtodom(StubHandler handler, int maxPages = 1) =>
        new(Factory(handler), Options(maxPages), NullLogger<OtodomScraper>.Instance);

    private static OlxScraper CreateOlx(StubHandler handler, int maxPages = 1) =>
        new(Factory(handler), Options(maxPages), NullLogger<OlxScraper>.Instance);

    private static IOptions<HomeSeekerOptions> Options(int maxPages) =>
        Microsoft.Extensions.Options.Options.Create(new HomeSeekerOptions
        {
            MaxSearchPagesPerPortal = maxPages,
            RequestDelay = TimeSpan.Zero
        });

    private static IHttpClientFactory Factory(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("homeseeker-scraper")).Returns(() => new HttpClient(handler, disposeHandler: false));
        return factory.Object;
    }

    /// <summary>
    /// Returns the queued responses in order; repeats the last one when exhausted.
    /// </summary>
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
