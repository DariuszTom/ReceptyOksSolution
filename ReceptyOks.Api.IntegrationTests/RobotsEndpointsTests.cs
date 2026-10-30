using System.Net;

namespace ReceptyOks.Api.IntegrationTests;

[TestFixture]
public class RobotsEndpointsTests
{
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void Setup()
    {
        _factory = new CustomWebApplicationFactory();
        // /robots.txt is exempt from the API-key middleware.
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task RobotsTxt_WithoutApiKey_ReturnsDisallowAll()
    {
        var response = await _client.GetAsync("/robots.txt");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/plain"));
            Assert.That(body, Does.Contain("User-agent: *"));
            Assert.That(body, Does.Contain("Disallow: /"));
        });
    }
}
