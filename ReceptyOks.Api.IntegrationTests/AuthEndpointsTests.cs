using HomeSeeker.Models;
using ReceptyOks.Api.Endpoints;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace ReceptyOks.Api.IntegrationTests;

[TestFixture]
public class AuthEndpointsTests
{
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void Setup()
    {
        _factory = new CustomWebApplicationFactory();
        // /api/auth is exempt from the API-key middleware, so a plain client suffices.
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static byte[] SecretBytes => Encoding.UTF8.GetBytes(CustomWebApplicationFactory.TestApiKey);

    [Test]
    public async Task Validate_WithCorrectSecretAndNoClientId_ReturnsValidWithoutToken()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/validate", new { SecretHash = SecretBytes });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Multiple(() =>
        {
            Assert.That(body!.IsValid, Is.True);
            Assert.That(body.Token, Is.Null);
            Assert.That(body.ExpiresAt, Is.Null);
        });
    }

    [Test]
    public async Task Validate_WithCorrectSecretAndClientId_ReturnsToken()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/validate", new { SecretHash = SecretBytes, ClientId = "device-1" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Multiple(() =>
        {
            Assert.That(body!.IsValid, Is.True);
            Assert.That(body.Token, Is.Not.Null.And.Not.Empty);
            Assert.That(body.ExpiresAt, Is.Not.Null);
            Assert.That(body.ExpiresAt!.Value, Is.GreaterThan(DateTime.UtcNow.AddMinutes(5)));
        });
    }

    [Test]
    public async Task Validate_WithWrongSecret_Returns401EvenWithClientId()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/validate",
            new { SecretHash = Encoding.UTF8.GetBytes("wrong"), ClientId = "device-1" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Validate_WithOverlongClientId_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/validate",
            new { SecretHash = SecretBytes, ClientId = new string('x', 201) });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task IssuedToken_GrantsAccessToHomeSeekerScopedToClientId()
    {
        var login = await _client.PostAsJsonAsync("/api/auth/validate", new { SecretHash = SecretBytes, ClientId = "device-42" });
        var token = (await login.Content.ReadFromJsonAsync<AuthResponse>())!.Token!;

        using var homeSeeker = _factory.CreateAuthenticatedClient();
        homeSeeker.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var create = await homeSeeker.PostAsJsonAsync("/api/homeseeker/profiles", new SearchProfileRequest
        {
            City = "Kraków",
            NotificationEmail = "t@e.com",
            MaxPrice = 500_000,
            IsActive = false
        });

        Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var profile = await create.Content.ReadFromJsonAsync<SearchProfile>();
        Assert.That(profile!.OwnerId, Is.EqualTo("device-42"));

        // A different device cannot see it.
        using var other = _factory.CreateAuthenticatedClient("device-other");
        var foreign = await other.GetAsync($"/api/homeseeker/profiles/{profile.Id}");
        Assert.That(foreign.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
