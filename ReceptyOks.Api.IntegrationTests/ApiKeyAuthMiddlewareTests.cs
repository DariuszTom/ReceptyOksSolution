using ReceptyOks.Shared;
using System.Net;

namespace ReceptyOks.Api.IntegrationTests;

[TestFixture]
public class ApiKeyAuthMiddlewareTests
{
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void Setup()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [TestCase("/healthz")]
    [TestCase("/alive-secret")]
    [TestCase("/api/authx")]
    public async Task InvokeAsync_PathOnlySharingAnonymousPrefix_Returns401(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task InvokeAsync_InvalidApiKey_Returns401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/categories");
        request.Headers.Add(GlobalConstants.ApiKeyHeaderName, "wrong-key");

        var response = await _client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task InvokeAsync_WhitespaceApiKey_Returns401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/categories");
        request.Headers.TryAddWithoutValidation(GlobalConstants.ApiKeyHeaderName, "   ");

        var response = await _client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task InvokeAsync_ValidApiKey_PassesThrough()
    {
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/categories");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
