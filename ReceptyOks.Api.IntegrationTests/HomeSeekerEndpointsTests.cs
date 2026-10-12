using HomeSeeker.Models;
using System.Net;
using System.Net.Http.Json;

namespace ReceptyOks.Api.IntegrationTests;

[TestFixture]
public class HomeSeekerEndpointsTests
{
    private const string OwnerA = "owner-a";
    private const string OwnerB = "owner-b";

    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _ownerA = null!;
    private HttpClient _ownerB = null!;

    [SetUp]
    public void Setup()
    {
        _factory = new CustomWebApplicationFactory();
        _ownerA = _factory.CreateAuthenticatedClient(OwnerA);
        _ownerB = _factory.CreateAuthenticatedClient(OwnerB);
    }

    [TearDown]
    public void TearDown()
    {
        _ownerA.Dispose();
        _ownerB.Dispose();
        _factory.Dispose();
    }

    private static SearchProfileRequest NewRequest(string city = "Warszawa") => new()
    {
        City = city,
        NotificationEmail = "test@example.com",
        MaxPrice = 1_000_000,
        IsActive = false
    };

    private async Task<SearchProfile> CreateProfileAsync(HttpClient client, string city = "Warszawa")
    {
        var response = await client.PostAsJsonAsync("/api/homeseeker/profiles", NewRequest(city));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var profile = await response.Content.ReadFromJsonAsync<SearchProfile>();
        Assert.That(profile, Is.Not.Null);
        return profile!;
    }

    [Test]
    public async Task GetProfiles_WithApiKeyButNoBearerToken_Returns401()
    {
        using var apiKeyOnly = _factory.CreateAuthenticatedClient();

        var response = await apiKeyOnly.GetAsync("/api/homeseeker/profiles");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GetProfiles_WithBearerTokenButNoApiKey_Returns401()
    {
        using var bearerOnly = _factory.CreateClient();
        bearerOnly.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CustomWebApplicationFactory.CreateJwt(OwnerA));

        var response = await bearerOnly.GetAsync("/api/homeseeker/profiles");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task CreateProfile_StampsCallerAsOwner()
    {
        var profile = await CreateProfileAsync(_ownerA);

        Assert.That(profile.OwnerId, Is.EqualTo(OwnerA));
    }

    [Test]
    public async Task GetProfiles_ReturnsOnlyCallersProfiles()
    {
        await CreateProfileAsync(_ownerA, "Warszawa");
        await CreateProfileAsync(_ownerA, "Krakow");
        await CreateProfileAsync(_ownerB, "Gdansk");

        var a = await _ownerA.GetFromJsonAsync<List<SearchProfile>>("/api/homeseeker/profiles");
        var b = await _ownerB.GetFromJsonAsync<List<SearchProfile>>("/api/homeseeker/profiles");

        Assert.Multiple(() =>
        {
            Assert.That(a, Has.Count.EqualTo(2));
            Assert.That(a!.Select(p => p.OwnerId), Is.All.EqualTo(OwnerA));
            Assert.That(b, Has.Count.EqualTo(1));
            Assert.That(b![0].City, Is.EqualTo("Gdansk"));
        });
    }

    [Test]
    public async Task GetProfile_OwnedByOtherCaller_Returns404()
    {
        var profile = await CreateProfileAsync(_ownerA);

        var own = await _ownerA.GetAsync($"/api/homeseeker/profiles/{profile.Id}");
        var foreign = await _ownerB.GetAsync($"/api/homeseeker/profiles/{profile.Id}");

        Assert.Multiple(() =>
        {
            Assert.That(own.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(foreign.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    [Test]
    public async Task UpdateProfile_OwnedByOtherCaller_Returns404AndDoesNotModify()
    {
        var profile = await CreateProfileAsync(_ownerA, "Warszawa");

        var foreign = await _ownerB.PutAsJsonAsync($"/api/homeseeker/profiles/{profile.Id}", NewRequest("Poznan"));
        var reloaded = await _ownerA.GetFromJsonAsync<SearchProfile>($"/api/homeseeker/profiles/{profile.Id}");

        Assert.Multiple(() =>
        {
            Assert.That(foreign.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(reloaded!.City, Is.EqualTo("Warszawa"));
        });
    }

    [Test]
    public async Task DeleteProfile_OwnedByOtherCaller_Returns404AndDoesNotDelete()
    {
        var profile = await CreateProfileAsync(_ownerA);

        var foreign = await _ownerB.DeleteAsync($"/api/homeseeker/profiles/{profile.Id}");
        var stillThere = await _ownerA.GetAsync($"/api/homeseeker/profiles/{profile.Id}");

        Assert.Multiple(() =>
        {
            Assert.That(foreign.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(stillThere.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    [Test]
    public async Task DeleteProfile_ByOwner_HidesItFromSubsequentReads()
    {
        var profile = await CreateProfileAsync(_ownerA);

        var delete = await _ownerA.DeleteAsync($"/api/homeseeker/profiles/{profile.Id}");
        var get = await _ownerA.GetAsync($"/api/homeseeker/profiles/{profile.Id}");
        var list = await _ownerA.GetFromJsonAsync<List<SearchProfile>>("/api/homeseeker/profiles");

        Assert.Multiple(() =>
        {
            Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(list, Is.Empty);
        });
    }

    [Test]
    public async Task ListingsAndScans_OwnedByOtherCaller_Return404()
    {
        var profile = await CreateProfileAsync(_ownerA);

        var listings = await _ownerB.GetAsync($"/api/homeseeker/profiles/{profile.Id}/listings");
        var scans = await _ownerB.GetAsync($"/api/homeseeker/profiles/{profile.Id}/scans");
        var trigger = await _ownerB.PostAsync($"/api/homeseeker/profiles/{profile.Id}/scan", content: null);

        Assert.Multiple(() =>
        {
            Assert.That(listings.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(scans.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(trigger.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    [Test]
    public async Task ListingsAndScans_ByOwner_ReturnOk()
    {
        var profile = await CreateProfileAsync(_ownerA);

        var listings = await _ownerA.GetAsync($"/api/homeseeker/profiles/{profile.Id}/listings");
        var scans = await _ownerA.GetAsync($"/api/homeseeker/profiles/{profile.Id}/scans");

        Assert.Multiple(() =>
        {
            Assert.That(listings.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(scans.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    [Test]
    public async Task CreateProfile_WithInvalidRequest_ReturnsValidationProblem()
    {
        var request = NewRequest() with { MinPrice = 500_000, MaxPrice = 100_000 };

        var response = await _ownerA.PostAsJsonAsync("/api/homeseeker/profiles", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
