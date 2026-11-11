using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using ReceptyOks.Configuration;
using ReceptyOks.Services;
using ReceptyOks.Shared;
using System.Net;
using System.Text;

namespace ReceptyOks_UnitTests.Services;

[TestFixture]
public class UpdateCheckerServiceTests
{
    private const string BaseUrl = "https://api.github.test/";
    private const string ReleaseEndpoint = "repos/owner/repo/releases/latest";

    private Mock<HttpMessageHandler> _mockHandler = null!;
    private HttpClient _httpClient = null!;
    private AppSettings _settings = null!;
    private UpdateCheckerService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _mockHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHandler.Object) { BaseAddress = new Uri(BaseUrl) };
        _settings = new AppSettings();
        _settings.Http.Github.ReleaseEndpoint = ReleaseEndpoint;

        _service = new UpdateCheckerService(_httpClient, _settings, Mock.Of<ILogger<UpdateCheckerService>>())
        {
            RetryDelayProvider = _ => TimeSpan.Zero
        };
    }

    [TearDown]
    public void TearDown() => _httpClient.Dispose();

    [Test]
    public void Constructor_NullHttpClient_ThrowsArgumentNullException()
    {
        Assert.That(
            () => new UpdateCheckerService(null!, _settings, Mock.Of<ILogger<UpdateCheckerService>>()),
            Throws.ArgumentNullException.With.Property(nameof(ArgumentNullException.ParamName)).EqualTo("httpClient"));
    }

    [Test]
    public void Constructor_NullSettings_ThrowsArgumentNullException()
    {
        Assert.That(
            () => new UpdateCheckerService(_httpClient, null!, Mock.Of<ILogger<UpdateCheckerService>>()),
            Throws.ArgumentNullException.With.Property(nameof(ArgumentNullException.ParamName)).EqualTo("settings"));
    }

    [Test]
    public async Task GetLatestReleaseAsync_SuccessResponse_ReturnsDeserializedRelease()
    {
        SetupResponses(JsonResponse("""
            {
              "tag_name": "v1.5.0",
              "html_url": "https://github.test/release",
              "assets": [ { "name": "app.apk", "browser_download_url": "https://github.test/app.apk" } ]
            }
            """));

        var release = await _service.GetLatestReleaseAsync();

        Assert.That(release, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(release!.TagName, Is.EqualTo("v1.5.0"));
            Assert.That(release.HtmlUrl, Is.EqualTo("https://github.test/release"));
            Assert.That(release.Assets, Has.Count.EqualTo(1));
            Assert.That(release.Assets![0].Name, Is.EqualTo("app.apk"));
            Assert.That(release.Assets[0].DownloadUrl, Is.EqualTo("https://github.test/app.apk"));
        });
    }

    [Test]
    public async Task GetLatestReleaseAsync_Called_RequestsConfiguredEndpointOnBaseAddress()
    {
        SetupResponses(JsonResponse("""{ "tag_name": "v1.0.0" }"""));

        await _service.GetLatestReleaseAsync();

        _mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r =>
                r.Method == HttpMethod.Get &&
                r.RequestUri == new Uri(new Uri(BaseUrl), ReleaseEndpoint)),
            ItExpr.IsAny<CancellationToken>());
    }

    [Test]
    public async Task GetLatestReleaseAsync_ServerAlwaysFails_RetriesAndReturnsNull()
    {
        SetupResponses(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var release = await _service.GetLatestReleaseAsync();

        Assert.That(release, Is.Null);
        VerifyRequestCount(GlobalConstants.MaxRetryAttempts + 1);
    }

    [Test]
    public async Task GetLatestReleaseAsync_TransientFailureThenSuccess_ReturnsRelease()
    {
        _mockHandler.Protected()
            .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))
            .ThrowsAsync(new HttpRequestException("network"))
            .ReturnsAsync(JsonResponse("""{ "tag_name": "v2.0.0" }"""));

        var release = await _service.GetLatestReleaseAsync();

        Assert.That(release?.TagName, Is.EqualTo("v2.0.0"));
        VerifyRequestCount(3);
    }

    [Test]
    public async Task GetLatestReleaseAsync_HttpRequestExceptionOnEveryAttempt_ReturnsNull()
    {
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("network down"));

        var release = await _service.GetLatestReleaseAsync();

        Assert.That(release, Is.Null);
        VerifyRequestCount(GlobalConstants.MaxRetryAttempts + 1);
    }

    [Test]
    public async Task GetLatestReleaseAsync_InvalidJson_ReturnsNull()
    {
        SetupResponses(JsonResponse("not json"));

        var release = await _service.GetLatestReleaseAsync();

        Assert.That(release, Is.Null);
    }

    [TestCase("v1.3.0", "1.2.0", true)]
    [TestCase("v2.0.0-beta", "1.9.9", true)]
    [TestCase("1.2.10", "1.2.9", true)]
    [TestCase("v1.2.0", "1.2.0", false)]
    [TestCase("v1.1.0", "1.2.0", false)]
    [TestCase("v1.2.0", "v1.2.0.alpha", false)]
    public async Task IsUpdateAvailableAsync_VersionComparison_ReturnsExpected(string serverTag, string appVersion, bool expected)
    {
        SetupResponses(JsonResponse($$"""{ "tag_name": "{{serverTag}}" }"""));

        var result = await _service.IsUpdateAvailableAsync(appVersion);

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public async Task IsUpdateAvailableAsync_ReleaseWithoutTag_ReturnsFalse()
    {
        SetupResponses(JsonResponse("{}"));

        var result = await _service.IsUpdateAvailableAsync("1.0.0");

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task IsUpdateAvailableAsync_UnparsableAppVersion_ReturnsFalse()
    {
        SetupResponses(JsonResponse("""{ "tag_name": "v1.0.0" }"""));

        var result = await _service.IsUpdateAvailableAsync("dev-build");

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task IsUpdateAvailableAsync_ReleaseUnavailable_ReturnsFalse()
    {
        SetupResponses(() => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await _service.IsUpdateAvailableAsync("1.0.0");

        Assert.That(result, Is.False);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private void SetupResponses(HttpResponseMessage response) => SetupResponses(() => response);

    private void SetupResponses(Func<HttpResponseMessage> responseFactory) =>
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(responseFactory);

    private void VerifyRequestCount(int expected) =>
        _mockHandler.Protected().Verify(
            "SendAsync",
            Times.Exactly(expected),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
}
