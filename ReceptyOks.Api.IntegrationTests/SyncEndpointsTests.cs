using System.Net;
using System.Net.Http.Json;
using ReceptyOks.Shared.DTOs;

namespace ReceptyOks.Api.IntegrationTests;

[TestFixture]
public class SyncEndpointsTests
{
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void Setup()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateAuthenticatedClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task Sync_WhenRequestIsNull_ReturnsBadRequest()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/sync", (SyncRequest)null!);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Sync_WhenValidEmptyRequest_ReturnsOk()
    {
        // Arrange
        var request = new SyncRequest
        {
            LastSyncedAt = DateTime.UtcNow.AddDays(-1),
            ChangedCategories = [],
            ChangedIngredients = [],
            ChangedRecipes = [],
            ChangedMealPlans = []
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/sync", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var syncResponse = await response.Content.ReadFromJsonAsync<SyncResponse>();
        Assert.That(syncResponse, Is.Not.Null);
        Assert.That(syncResponse!.SyncedAt, Is.GreaterThan(DateTime.MinValue));
    }

    [Test]
    public async Task Sync_WhenCategoryNameIsEmpty_ReturnsValidationError()
    {
        // Arrange
        var request = new SyncRequest
        {
            LastSyncedAt = DateTime.UtcNow.AddDays(-1),
            ChangedCategories =
            [
                new CategorySyncDto
                {
                    Id = Guid.NewGuid(),
                    Name = "", // Invalid: empty name
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    UpdatedAt = DateTime.UtcNow,
                    IsDeleted = false
                }
            ],
            ChangedIngredients = [],
            ChangedRecipes = [],
            ChangedMealPlans = []
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/sync", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problemDetails = await response.Content.ReadAsStringAsync();
        Assert.That(problemDetails, Does.Contain("Name"));
    }

    [Test]
    public async Task Sync_WhenCategoryIdIsEmpty_ReturnsValidationError()
    {
        // Arrange
        var request = new SyncRequest
        {
            LastSyncedAt = DateTime.UtcNow.AddDays(-1),
            ChangedCategories =
            [
                new CategorySyncDto
                {
                    Id = Guid.Empty, // Invalid: empty GUID
                    Name = "Test Category",
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    UpdatedAt = DateTime.UtcNow,
                    IsDeleted = false
                }
            ],
            ChangedIngredients = [],
            ChangedRecipes = [],
            ChangedMealPlans = []
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/sync", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problemDetails = await response.Content.ReadAsStringAsync();
        Assert.That(problemDetails, Does.Contain("Id"));
    }

    [Test]
    public async Task Sync_WhenIngredientNameIsTooLong_ReturnsValidationError()
    {
        // Arrange
        var request = new SyncRequest
        {
            LastSyncedAt = DateTime.UtcNow.AddDays(-1),
            ChangedCategories = [],
            ChangedIngredients =
            [
                new IngredientSyncDto
                {
                    Id = Guid.NewGuid(),
                    Name = new string('a', 101), // Invalid: exceeds 100 chars
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    UpdatedAt = DateTime.UtcNow,
                    IsDeleted = false
                }
            ],
            ChangedRecipes = [],
            ChangedMealPlans = []
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/sync", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problemDetails = await response.Content.ReadAsStringAsync();
        Assert.That(problemDetails, Does.Contain("Name"));
    }

    [Test]
    public async Task Sync_WhenRecipeTitleIsEmpty_ReturnsValidationError()
    {
        // Arrange
        var request = new SyncRequest
        {
            LastSyncedAt = DateTime.UtcNow.AddDays(-1),
            ChangedCategories = [],
            ChangedIngredients = [],
            ChangedRecipes =
            [
                new RecipeSyncDto
                {
                    Id = Guid.NewGuid(),
                    Title = "", // Invalid: empty title
                    Description = "Test",
                    Instructions = "Test",
                    Servings = 4,
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    UpdatedAt = DateTime.UtcNow,
                    IsDeleted = false,
                    Ingredients = []
                }
            ],
            ChangedMealPlans = []
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/sync", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problemDetails = await response.Content.ReadAsStringAsync();
        Assert.That(problemDetails, Does.Contain("Title"));
    }

    [Test]
    public async Task UploadAll_WhenRequestIsNull_ReturnsBadRequest()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/sync/upload-all", (SyncRequest)null!);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task UploadAll_WhenValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new SyncRequest
        {
            ChangedCategories =
            [
                new CategorySyncDto
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Category",
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    UpdatedAt = DateTime.UtcNow,
                    IsDeleted = false
                }
            ],
            ChangedIngredients = [],
            ChangedRecipes = [],
            ChangedMealPlans = []
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/sync/upload-all", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var syncResponse = await response.Content.ReadFromJsonAsync<SyncResponse>();
        Assert.That(syncResponse, Is.Not.Null);
        Assert.That(syncResponse!.SyncedAt, Is.GreaterThan(DateTime.MinValue));
    }

    [Test]
    public async Task FullSync_WhenUnauthorized_Returns401()
    {
        // Arrange
        using var unauthenticatedClient = _factory.CreateClient();

        // Act
        var response = await unauthenticatedClient.GetAsync("/api/sync/full");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task FullSync_WhenAuthorized_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/sync/full");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var syncResponse = await response.Content.ReadFromJsonAsync<SyncResponse>();
        Assert.That(syncResponse, Is.Not.Null);
        Assert.That(syncResponse!.Categories, Is.Not.Null);
        Assert.That(syncResponse.Ingredients, Is.Not.Null);
        Assert.That(syncResponse.Recipes, Is.Not.Null);
        Assert.That(syncResponse.MealPlans, Is.Not.Null);
    }
}
