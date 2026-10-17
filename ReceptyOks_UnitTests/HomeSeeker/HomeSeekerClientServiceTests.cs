using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReceptyOks.Models.HomeSeeker;
using ReceptyOks.Services;
using ReceptyOks.ViewModels;
using ServerModels = HomeSeeker.Models;

namespace ReceptyOks_UnitTests.HomeSeeker;

/// <summary>
/// Tests the MAUI client for <c>/api/homeseeker</c>: status mapping, JWT refresh-and-retry,
/// and wire compatibility of the client DTOs with the server models.
/// </summary>
public sealed class HomeSeekerClientServiceTests
{
    // Mirrors ReceptyOks.Api Program.cs ConfigureHttpJsonOptions on top of the minimal-API web defaults.
    private static readonly JsonSerializerOptions ServerJson = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Test]
    public async Task GetProfileAsync_404_ReturnsNotFound()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var sut = CreateSut(handler, out _);

        var result = await sut.GetProfileAsync();

        Assert.That(result.Status, Is.EqualTo(HomeSeekerCallStatus.NotFound));
        Assert.That(handler.Requests.Single().RequestUri!.AbsolutePath, Is.EqualTo("/api/homeseeker/profile"));
    }

    [Test]
    public async Task GetProfileAsync_ServerProfile_DeserializesIntoClientDto()
    {
        var server = new ServerModels.SearchProfile
        {
            Id = Guid.NewGuid(),
            City = "Wrocław",
            PropertyTypes = ServerModels.PropertyTypes.Apartment,
            MinPrice = 500_000,
            MaxAreaSqm = 180.5m,
            NotificationEmail = "a@example.com; b@example.com",
            IsActive = false,
            LastScannedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)
        };
        var sut = CreateSut(new StubHandler(_ => Json(HttpStatusCode.OK, server)), out _);

        var result = await sut.GetProfileAsync();

        Assert.That(result.IsSuccess, Is.True);
        var profile = result.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(profile.Id, Is.EqualTo(server.Id));
            Assert.That(profile.City, Is.EqualTo("Wrocław"));
            Assert.That(profile.PropertyTypes, Is.EqualTo(HomeSeekerPropertyTypes.Apartment));
            Assert.That(profile.MinPrice, Is.EqualTo(500_000m));
            Assert.That(profile.MaxAreaSqm, Is.EqualTo(180.5m));
            Assert.That(profile.NotificationEmail, Is.EqualTo(server.NotificationEmail));
            Assert.That(profile.IsActive, Is.False);
            Assert.That(profile.LastScannedAt, Is.EqualTo(server.LastScannedAt));
        });
    }

    [Test]
    public async Task SaveProfileAsync_400_ReturnsValidationErrors()
    {
        const string problem = """
            {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,
             "errors":{"City":["Miasto jest wymagane."],"NotificationEmail":["Nieprawidłowy e-mail.","Za dużo adresów."]}}
            """;
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(problem, Encoding.UTF8, "application/problem+json")
        });
        var sut = CreateSut(handler, out _);

        var result = await sut.SaveProfileAsync(new HomeSeekerProfileRequest());

        Assert.That(result.Status, Is.EqualTo(HomeSeekerCallStatus.ValidationFailed));
        Assert.That(result.Errors["city"], Is.EqualTo(new[] { "Miasto jest wymagane." }));
        Assert.That(result.Errors["NotificationEmail"], Has.Length.EqualTo(2));
        Assert.That(handler.Requests.Single().Method, Is.EqualTo(HttpMethod.Put));
    }

    [Test]
    public async Task SaveProfileAsync_SendsBodyMatchingServerRequestDto()
    {
        string? body = null;
        var handler = new StubHandler(request =>
        {
            body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Json(HttpStatusCode.OK, new ServerModels.SearchProfile { City = "Kraków", NotificationEmail = "a@example.com" });
        });
        var sut = CreateSut(handler, out _);

        await sut.SaveProfileAsync(new HomeSeekerProfileRequest
        {
            City = "Kraków",
            PropertyTypes = HomeSeekerPropertyTypes.All,
            MinPrice = 400_000,
            ExtraCriteria = "ogród",
            NotificationEmail = "a@example.com",
            IsActive = false
        });

        var server = JsonSerializer.Deserialize<ServerModels.SearchProfileRequest>(body!, ServerJson)!;
        Assert.Multiple(() =>
        {
            Assert.That(server.City, Is.EqualTo("Kraków"));
            Assert.That(server.PropertyTypes, Is.EqualTo(ServerModels.PropertyTypes.All));
            Assert.That(server.MinPrice, Is.EqualTo(400_000m));
            Assert.That(server.ExtraCriteria, Is.EqualTo("ogród"));
            Assert.That(server.NotificationEmail, Is.EqualTo("a@example.com"));
            Assert.That(server.IsActive, Is.False);
        });
    }

    [Test]
    public async Task Request_401_RefreshesTokenAndRetriesOnce()
    {
        var handler = new StubHandler(
            _ => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            _ => Json(HttpStatusCode.OK, Array.Empty<ServerModels.ScanRun>()));
        var sut = CreateSut(handler, out var refresher);
        refresher.Setup(r => r.RefreshAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await sut.GetScansAsync();

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(handler.Requests, Has.Count.EqualTo(2));
        refresher.Verify(r => r.RefreshAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Request_401_RefreshFails_ReturnsUnauthorizedWithoutRetry()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var sut = CreateSut(handler, out var refresher);
        refresher.Setup(r => r.RefreshAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await sut.GetScansAsync();

        Assert.That(result.Status, Is.EqualTo(HomeSeekerCallStatus.Unauthorized));
        Assert.That(handler.Requests, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Request_NoStoredToken_RefreshesBeforeSending()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, Array.Empty<ServerModels.ScanRun>()));
        var sut = CreateSut(handler, out var refresher, hasToken: false);
        refresher.Setup(r => r.RefreshAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await sut.GetScansAsync();

        refresher.Verify(r => r.RefreshAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(handler.Requests, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Request_NetworkFailure_ReturnsError()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("offline"));
        var sut = CreateSut(handler, out _);

        var result = await sut.GetProfileAsync();

        Assert.That(result.Status, Is.EqualTo(HomeSeekerCallStatus.Error));
    }

    [Test]
    public async Task GetListingsAsync_BuildsQueryAndMapsServerListing()
    {
        var listing = new ServerModels.HouseListing
        {
            Id = Guid.NewGuid(),
            Portal = "Otodom",
            Url = "https://www.otodom.pl/pl/oferta/dom-1",
            Title = "Dom z ogrodem",
            Price = 600_000,
            PreviousPrice = 650_000,
            AreaSqm = 120,
            AiScore = 82,
            AiProsJson = """["Duży ogród","Cicha okolica"]""",
            AiConsJson = """["Do remontu"]""",
            DistanceFromCenterKm = 4.25,
            DistanceIsApproximate = true
        };
        var page = new { TotalCount = 41, PageNumber = 2, PageSize = 20, TotalPages = 3, Data = new[] { listing } };
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, page));
        var sut = CreateSut(handler, out _);

        var result = await sut.GetListingsAsync(2, 20, new HomeSeekerListingQuery { MinScore = 70, Sort = "price" });

        Assert.That(handler.Requests.Single().RequestUri!.PathAndQuery,
            Is.EqualTo("/api/homeseeker/listings?pageNumber=2&pageSize=20&minScore=70&sort=price"));
        var value = result.Value!;
        Assert.That(value.TotalPages, Is.EqualTo(3));
        var item = value.Data.Single();
        Assert.Multiple(() =>
        {
            Assert.That(item.Title, Is.EqualTo("Dom z ogrodem"));
            Assert.That(item.HasPriceDrop, Is.True);
            Assert.That(item.PricePerSqm, Is.EqualTo(5000m));
            Assert.That(item.Pros, Is.EqualTo(new[] { "Duży ogród", "Cicha okolica" }));
            Assert.That(item.Cons, Is.EqualTo(new[] { "Do remontu" }));
            Assert.That(item.HasHttpUrl, Is.True);
            Assert.That(item.DistanceFromCenterKm, Is.EqualTo(4.25));
            Assert.That(item.DistanceIsApproximate, Is.True);
            Assert.That(item.DistanceText, Does.StartWith("ok. ").And.EndWith(" km"));
        });
    }

    [Test]
    public async Task GetListingsAsync_SendsAllFilters()
    {
        var page = new { TotalCount = 0, PageNumber = 1, PageSize = 20, TotalPages = 0, Data = Array.Empty<object>() };
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, page));
        var sut = CreateSut(handler, out _);

        await sut.GetListingsAsync(1, 20, new HomeSeekerListingQuery
        {
            Sort = "distance",
            PropertyType = HomeSeekerPropertyTypes.Apartment,
            Portal = "OLX",
            MaxDistanceKm = 5,
            PriceDropOnly = true,
            EvaluatedOnly = true
        });

        Assert.That(handler.Requests.Single().RequestUri!.PathAndQuery,
            Is.EqualTo("/api/homeseeker/listings?pageNumber=1&pageSize=20&sort=distance&propertyType=2&portal=OLX&maxDistanceKm=5&priceDropOnly=true&evaluatedOnly=true"));
    }

    [Test]
    public async Task GetMapListingsAsync_SendsLimitAndFilters_WithoutSort_AndMapsCoordinates()
    {
        var server = new[]
        {
            ServerModels.MapListing.From(new ServerModels.HouseListing
            {
                Id = Guid.NewGuid(),
                Title = "Mieszkanie",
                Url = "https://www.olx.pl/d/oferta/x",
                Price = 500_000,
                AreaSqm = 50,
                AiScore = 77,
                PropertyType = ServerModels.PropertyTypes.Apartment,
                Latitude = 51.1,
                Longitude = 17.03,
                CoordinatesApproximate = true
            })
        };
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, server));
        var sut = CreateSut(handler, out _);

        var result = await sut.GetMapListingsAsync(50, new HomeSeekerListingQuery
        {
            MinScore = 60,
            Sort = "price",
            PropertyType = HomeSeekerPropertyTypes.Apartment,
            Portal = "OLX",
            EvaluatedOnly = false
        });

        Assert.That(handler.Requests.Single().RequestUri!.PathAndQuery,
            Is.EqualTo("/api/homeseeker/map?limit=50&minScore=60&propertyType=2&portal=OLX"));
        var item = result.Value!.Single();
        Assert.Multiple(() =>
        {
            Assert.That(item.Latitude, Is.EqualTo(51.1));
            Assert.That(item.Longitude, Is.EqualTo(17.03));
            Assert.That(item.CoordinatesApproximate, Is.True);
            Assert.That(item.PropertyType, Is.EqualTo(HomeSeekerPropertyTypes.Apartment));
            Assert.That(item.PricePerSqm, Is.EqualTo(10_000m));
            Assert.That(item.HasHttpUrl, Is.True);
        });
    }

    [Test]
    public async Task GetScansAsync_MapsNumericStatus()
    {
        // Mirrors ListingRepository.GetScanHistoryAsync: ReportHtml is omitted, only HasReport is projected.
        var scans = new[]
        {
            new ServerModels.ScanRun { Id = Guid.NewGuid(), Status = ServerModels.ScanStatus.Failed, Error = "boom", HasReport = false },
            new ServerModels.ScanRun { Id = Guid.NewGuid(), Status = ServerModels.ScanStatus.Completed, HasReport = true, NewListingsCount = 3 }
        };
        var sut = CreateSut(new StubHandler(_ => Json(HttpStatusCode.OK, scans)), out _);

        var result = await sut.GetScansAsync(count: 5);

        var value = result.Value!;
        Assert.That(value[0].Status, Is.EqualTo(HomeSeekerScanStatus.Failed));
        Assert.That(value[0].IsFailed, Is.True);
        Assert.That(value[1].Status, Is.EqualTo(HomeSeekerScanStatus.Completed));
        Assert.That(value[1].HasReport, Is.True);
        Assert.That(value[1].NewListingsCount, Is.EqualTo(3));
    }

    [Test]
    public async Task GetReportHtmlAsync_ReturnsBody()
    {
        var id = Guid.NewGuid();
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html><body>raport</body></html>", Encoding.UTF8, "text/html")
        });
        var sut = CreateSut(handler, out _);

        var result = await sut.GetReportHtmlAsync(id);

        Assert.That(result.Value, Does.Contain("raport"));
        Assert.That(handler.Requests.Single().RequestUri!.AbsolutePath, Is.EqualTo($"/api/homeseeker/scans/{id}/report"));
    }

    [TestCase(null, 0)]
    [TestCase("", 0)]
    [TestCase("not json", 0)]
    [TestCase("""["a",""," ","b"]""", 2)]
    public void Listing_ParseList_IsTolerant(string? json, int expected)
    {
        Assert.That(HomeSeekerListing.ParseList(json), Has.Count.EqualTo(expected));
    }

    [TestCase("javascript:alert(1)", false)]
    [TestCase("file:///etc/passwd", false)]
    [TestCase("http://olx.pl/x", true)]
    public void Listing_HasHttpUrl_OnlyAllowsHttpSchemes(string url, bool expected)
    {
        Assert.That(new HomeSeekerListing { Url = url }.HasHttpUrl, Is.EqualTo(expected));
    }

    [Test]
    public void ServerScanRun_HasReport_DerivesFromHtmlUnlessProjected()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new ServerModels.ScanRun { ReportHtml = "<p>x</p>" }.HasReport, Is.True);
            Assert.That(new ServerModels.ScanRun().HasReport, Is.False);
            Assert.That(new ServerModels.ScanRun { HasReport = true }.HasReport, Is.True);
        });
    }

    [TestCase("1 250 000", 1_250_000)]
    [TestCase("450000", 450_000)]
    [TestCase("120.5", 120.5)]
    [TestCase("120,5", 120.5)]
    [TestCase("-5", -5)]
    public void ViewModel_ParseDecimal_AcceptsCommonFormats_InAnyCulture(string text, decimal expected)
    {
        foreach (var culture in new[] { "en-US", "de-DE", "pl-PL" })
        {
            var errors = new List<string>();
            var value = WithCulture(culture, () => HomeSeekerViewModel.ParseDecimal(text, "Pole", errors));

            if (expected < 0)
            {
                Assert.That(value, Is.Null, culture);
                Assert.That(errors.Single(), Does.Contain("ujemna"), culture);
            }
            else
            {
                Assert.That(value, Is.EqualTo(expected), culture);
                Assert.That(errors, Is.Empty, culture);
            }
        }
    }

    [TestCase("1,250,000")]
    [TestCase("1.250.000")]
    [TestCase("1.250,5")]
    public void ViewModel_ParseDecimal_RejectsGroupSeparators(string text)
    {
        foreach (var culture in new[] { "en-US", "de-DE", "pl-PL" })
        {
            var errors = new List<string>();
            var value = WithCulture(culture, () => HomeSeekerViewModel.ParseDecimal(text, "Pole", errors));

            Assert.That(value, Is.Null, culture);
            Assert.That(errors, Has.Count.EqualTo(1), culture);
        }
    }

    private static T WithCulture<T>(string name, Func<T> action)
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(name);
        try { return action(); }
        finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
    }

    [Test]
    public void ViewModel_ParseDecimal_InvalidText_AddsError()
    {
        var errors = new List<string>();

        var value = HomeSeekerViewModel.ParseDecimal("abc", "Cena min", errors);

        Assert.That(value, Is.Null);
        Assert.That(errors.Single(), Does.StartWith("Cena min"));
    }

    [Test]
    public void Report_WithContentSecurityPolicy_InjectsMetaIntoHead()
    {
        var html = HomeSeekerReportViewModel.WithContentSecurityPolicy("<html><head><title>R</title></head><body>x</body></html>");

        Assert.That(html, Does.StartWith("<html><head><meta http-equiv=\"Content-Security-Policy\""));
        Assert.That(html, Does.Contain("default-src 'none'"));
    }

    [Test]
    public void Report_WithContentSecurityPolicy_WrapsFragment()
    {
        var html = HomeSeekerReportViewModel.WithContentSecurityPolicy("<p>x</p>");

        Assert.That(html, Does.Contain(HomeSeekerReportViewModel.ContentSecurityPolicyMeta));
        Assert.That(html, Does.Contain("<body><p>x</p></body>"));
    }

    private static HomeSeekerService CreateSut(StubHandler handler, out Mock<IAccessTokenRefresher> refresher, bool hasToken = true)
    {
        refresher = new Mock<IAccessTokenRefresher>();
        refresher.Setup(r => r.HasValidTokenAsync()).ReturnsAsync(hasToken);

        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
        return new HomeSeekerService(client, refresher.Object, NullLogger<HomeSeekerService>.Instance);
    }

    private static HttpResponseMessage Json<T>(HttpStatusCode status, T value) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(value, ServerJson), Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var index = Math.Min(Requests.Count - 1, responses.Length - 1);
            return Task.FromResult(responses[index](request));
        }
    }
}
