using HomeSeeker.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace ReceptyOks.Api.IntegrationTests;

[TestFixture]
public class HomeSeekerEndpointsTests
{
    private const string UserA = "user-a";
    private const string UserB = "user-b";

    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _userA = null!;
    private HttpClient _userB = null!;

    [SetUp]
    public void Setup()
    {
        _factory = new CustomWebApplicationFactory();
        _userA = _factory.CreateAuthenticatedClient(UserA);
        _userB = _factory.CreateAuthenticatedClient(UserB);
    }

    [TearDown]
    public void TearDown()
    {
        _userA.Dispose();
        _userB.Dispose();
        _factory.Dispose();
    }

    private static SearchProfileRequest NewRequest(string city = "Warszawa") => new()
    {
        City = city,
        NotificationEmail = "test@example.com",
        MaxPrice = 1_000_000,
        IsActive = false
    };

    private static async Task<SearchProfile> PutProfileAsync(HttpClient client, string city = "Warszawa")
    {
        var response = await client.PutAsJsonAsync("/api/homeseeker/profile", NewRequest(city));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var profile = await response.Content.ReadFromJsonAsync<SearchProfile>();
        Assert.That(profile, Is.Not.Null);
        return profile!;
    }

    [Test]
    public async Task GetProfile_WithApiKeyButNoBearerToken_Returns401()
    {
        using var apiKeyOnly = _factory.CreateAuthenticatedClient();

        var response = await apiKeyOnly.GetAsync("/api/homeseeker/profile");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GetProfile_WithBearerTokenButNoApiKey_Returns401()
    {
        using var bearerOnly = _factory.CreateClient();
        bearerOnly.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CustomWebApplicationFactory.CreateJwt(UserA));

        var response = await bearerOnly.GetAsync("/api/homeseeker/profile");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GetProfile_BeforeConfigured_Returns404()
    {
        var response = await _userA.GetAsync("/api/homeseeker/profile");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task PutProfile_FirstTime_CreatesSharedProfileAndStampsEditor()
    {
        var profile = await PutProfileAsync(_userA);

        var reloaded = await _userA.GetFromJsonAsync<SearchProfile>("/api/homeseeker/profile");

        Assert.Multiple(() =>
        {
            Assert.That(profile.OwnerId, Is.EqualTo(UserA));
            Assert.That(profile.City, Is.EqualTo("Warszawa"));
            Assert.That(reloaded!.Id, Is.EqualTo(profile.Id));
        });
    }

    [Test]
    public async Task PutProfile_ByAnotherUser_UpdatesTheSameSharedProfile()
    {
        var first = await PutProfileAsync(_userA, "Warszawa");
        var second = await PutProfileAsync(_userB, "Krakow");

        var seenByA = await _userA.GetFromJsonAsync<SearchProfile>("/api/homeseeker/profile");

        Assert.Multiple(() =>
        {
            Assert.That(second.Id, Is.EqualTo(first.Id));
            Assert.That(second.OwnerId, Is.EqualTo(UserB));
            Assert.That(seenByA!.City, Is.EqualTo("Krakow"));
            Assert.That(seenByA.OwnerId, Is.EqualTo(UserB));
        });
    }

    [Test]
    public async Task PutProfile_WithInvalidRequest_ReturnsValidationProblem()
    {
        var request = NewRequest() with { MinPrice = 500_000, MaxPrice = 100_000 };

        var response = await _userA.PutAsJsonAsync("/api/homeseeker/profile", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PutProfile_WithMultipleRecipients_NormalizesList()
    {
        var request = NewRequest() with { NotificationEmail = "mom@example.com; dad@example.com ,kid@example.com" };

        var response = await _userA.PutAsJsonAsync("/api/homeseeker/profile", request);
        var profile = await response.Content.ReadFromJsonAsync<SearchProfile>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(profile!.NotificationEmail, Is.EqualTo("mom@example.com, dad@example.com, kid@example.com"));
        });
    }

    [TestCase("not-an-email")]
    [TestCase("ok@example.com, broken")]
    [TestCase("a@x.pl,b@x.pl,c@x.pl,d@x.pl,e@x.pl,f@x.pl")]
    [TestCase(",;")]
    [TestCase("  ;  ,  ")]
    public async Task PutProfile_WithInvalidRecipients_ReturnsValidationProblem(string emails)
    {
        var request = NewRequest() with { NotificationEmail = emails };

        var response = await _userA.PutAsJsonAsync("/api/homeseeker/profile", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PutProfile_WhenNormalizedListExceedsLimit_ReturnsValidationProblem()
    {
        // Raw input fits in the column (199 chars, "," separated) but the stored
        // normalized form (", " separated) would be 203 chars.
        var emails = string.Join(",", Enumerable.Range(0, 5)
            .Select(i => $"{(char)('a' + i)}{new string('x', 33)}@x.pl"));
        Assume.That(emails.Length, Is.LessThanOrEqualTo(NotificationEmails.MaxLength));
        Assume.That(NotificationEmails.Normalize(emails).Length, Is.GreaterThan(NotificationEmails.MaxLength));

        var request = NewRequest() with { NotificationEmail = emails };

        var response = await _userA.PutAsJsonAsync("/api/homeseeker/profile", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task ListingsAndScans_BeforeConfigured_ReturnEmptyOk()
    {
        var listings = await _userA.GetAsync("/api/homeseeker/listings");
        var scans = await _userA.GetFromJsonAsync<List<ScanRun>>("/api/homeseeker/scans");

        Assert.Multiple(() =>
        {
            Assert.That(listings.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(scans, Is.Empty);
        });
    }

    [Test]
    public async Task ListingsAndScans_AreSharedBetweenUsers()
    {
        await PutProfileAsync(_userA);

        var listingsA = await _userA.GetAsync("/api/homeseeker/listings");
        var listingsB = await _userB.GetAsync("/api/homeseeker/listings");
        var scansA = await _userA.GetAsync("/api/homeseeker/scans");
        var scansB = await _userB.GetAsync("/api/homeseeker/scans");

        Assert.Multiple(() =>
        {
            Assert.That(listingsA.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(listingsB.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(scansA.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(scansB.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    [Test]
    public async Task ScanHistory_ReportsHasReportWithoutReturningHtml()
    {
        var profile = await PutProfileAsync(_userA);
        Guid withReport, withoutReport;
        using (var scope = _factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<HomeSeeker.Abstractions.IListingRepository>();
            withReport = (await repository.CreateScanRunAsync(profile.Id)).Id;
            await repository.CompleteScanRunAsync(withReport, ScanStatus.Completed, 1, 1, 0, 1, "<p>raport</p>", null);
            withoutReport = (await repository.CreateScanRunAsync(profile.Id)).Id;
            await repository.CompleteScanRunAsync(withoutReport, ScanStatus.Failed, 0, 0, 0, 0, null, "boom");
        }

        var json = await _userA.GetStringAsync("/api/homeseeker/scans");
        var scans = System.Text.Json.JsonSerializer.Deserialize<List<ScanRun>>(json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Not.Contain("raport"), "History must not ship report HTML.");
            Assert.That(scans.Single(s => s.Id == withReport).HasReport, Is.True);
            Assert.That(scans.Single(s => s.Id == withoutReport).HasReport, Is.False);
        });
    }

    [Test]
    public async Task RemovedPerProfileEndpoints_Return404()
    {
        var profile = await PutProfileAsync(_userA);

        var create = await _userA.PostAsJsonAsync("/api/homeseeker/profiles", NewRequest());
        var trigger = await _userA.PostAsync($"/api/homeseeker/profiles/{profile.Id}/scan", content: null);
        var delete = await _userA.DeleteAsync($"/api/homeseeker/profiles/{profile.Id}");

        Assert.Multiple(() =>
        {
            Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(trigger.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    [Test]
    public async Task GetScanReport_UnknownScan_Returns404()
    {
        var response = await _userA.GetAsync($"/api/homeseeker/scans/{Guid.NewGuid()}/report");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
