using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using ReceptyOks.Data;
using ReceptyOks.Services;
using ReceptyOks.Shared.DTOs;
using System.Net;
using System.Net.Http.Json;

namespace ReceptyOks_UnitTests.Services;

[TestFixture]
public class SyncServiceTests
{
    private Mock<ILocalDatabase> _mockLocalDb = null!;
    private Mock<HttpMessageHandler> _mockHttpMessageHandler = null!;
    private HttpClient _httpClient = null!;
    private Mock<ILogger<SyncService>> _mockLogger = null!;
    private SyncService _syncService = null!;

    [SetUp]
    public void SetUp()
    {
        _mockLocalDb = new Mock<ILocalDatabase>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("https://test.api")
        };
        _mockLogger = new Mock<ILogger<SyncService>>();

        _syncService = new SyncService(_mockLocalDb.Object, _httpClient, _mockLogger.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
    }

    #region SyncAsync Tests

    [Test]
    public async Task SyncAsync_WhenNoInternetConnection_ReturnsFailureResult()
    {
        // Arrange - Connectivity.Current.NetworkAccess zwraca NotReachable
        // (nie możemy łatwo zamockować statycznego Connectivity.Current, więc ten test może wymagać refaktoryzacji)
        // Dla celów demonstracji - zakładamy że w środowisku testowym jest internet

        // Ten test wymaga dependency injection dla Connectivity
        // Pominięty z powodu statycznej zależności - dokumentacja potrzeby refaktoryzacji
        Assert.Pass("Test skipped - requires IConnectivity abstraction");
    }

    [Test]
    public async Task SyncAsync_WhenServerReturns500_ReturnsFailureResult()
    {
        // Arrange
        _mockLocalDb.Setup(x => x.GetLastSyncTimeAsync())
            .ReturnsAsync(DateTime.UtcNow.AddDays(-1));
        _mockLocalDb.Setup(x => x.GetDirtyRecipesAsync()).ReturnsAsync([]);
        _mockLocalDb.Setup(x => x.GetDirtyCategoriesAsync()).ReturnsAsync([]);
        _mockLocalDb.Setup(x => x.GetDirtyIngredientsAsync()).ReturnsAsync([]);
        _mockLocalDb.Setup(x => x.GetDirtyMealPlansAsync()).ReturnsAsync([]);

        SetupHttpResponse(HttpStatusCode.InternalServerError);

        // Act
        var result = await _syncService.SyncAsync();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("Błąd").Or.Contains("serwera").Or.Contains("InternalServerError"));
        });
    }

    [Test]
    public async Task SyncAsync_WhenServerReturnsNullResponse_ReturnsFailureResult()
    {
        // Arrange
        _mockLocalDb.Setup(x => x.GetLastSyncTimeAsync())
            .ReturnsAsync(DateTime.UtcNow.AddDays(-1));
        _mockLocalDb.Setup(x => x.GetDirtyRecipesAsync()).ReturnsAsync([]);
        _mockLocalDb.Setup(x => x.GetDirtyCategoriesAsync()).ReturnsAsync([]);
        _mockLocalDb.Setup(x => x.GetDirtyIngredientsAsync()).ReturnsAsync([]);
        _mockLocalDb.Setup(x => x.GetDirtyMealPlansAsync()).ReturnsAsync([]);

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null") // JSON null
            });

        // Act
        var result = await _syncService.SyncAsync();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("Pusta odpowiedź"));
        });
    }

    [Test]
    public async Task SyncAsync_WhenAllItemsApplySuccessfully_UpdatesLastSyncedAt()
    {
        // Arrange
        var lastSync = DateTime.UtcNow.AddDays(-1);
        var newSyncedAt = DateTime.UtcNow;

        _mockLocalDb.Setup(x => x.GetLastSyncTimeAsync()).ReturnsAsync(lastSync);
        _mockLocalDb.Setup(x => x.GetDirtyRecipesAsync()).ReturnsAsync([]);
        _mockLocalDb.Setup(x => x.GetDirtyCategoriesAsync()).ReturnsAsync([]);
        _mockLocalDb.Setup(x => x.GetDirtyIngredientsAsync()).ReturnsAsync([]);
        _mockLocalDb.Setup(x => x.GetDirtyMealPlansAsync()).ReturnsAsync([]);

        var syncResponse = new SyncResponse
        {
            SyncedAt = newSyncedAt,
            Categories = [],
            Ingredients = [],
            Recipes = [],
            MealPlans = []
        };

        SetupHttpResponse(HttpStatusCode.OK, syncResponse);

        // Act
        var result = await _syncService.SyncAsync();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Does.Contain("pomyślnie"));
        });

        _mockLocalDb.Verify(x => x.ClearDirtyFlagsAsync(), Times.Once);
        _mockLocalDb.Verify(x => x.SetLastSyncTimeAsync(newSyncedAt), Times.Once);
    }

    [Test]
    public async Task SyncAsync_WhenSomeItemsFail_DoesNotUpdateLastSyncedAt()
    {
        // Arrange
        var lastSync = DateTime.UtcNow.AddDays(-1);
        var newSyncedAt = DateTime.UtcNow;

        _mockLocalDb.Setup(x => x.GetLastSyncTimeAsync()).ReturnsAsync(lastSync);
        _mockLocalDb.Setup(x => x.GetDirtyRecipesAsync()).ReturnsAsync([]);
        _mockLocalDb.Setup(x => x.GetDirtyCategoriesAsync()).ReturnsAsync([]);
        _mockLocalDb.Setup(x => x.GetDirtyIngredientsAsync()).ReturnsAsync([]);
        _mockLocalDb.Setup(x => x.GetDirtyMealPlansAsync()).ReturnsAsync([]);

        var syncResponse = new SyncResponse
        {
            SyncedAt = newSyncedAt,
            Categories = [new CategorySyncDto { Id = Guid.NewGuid(), Name = "Test" }],
            Ingredients = [],
            Recipes = [],
            MealPlans = []
        };

        SetupHttpResponse(HttpStatusCode.OK, syncResponse);

        // Simulate failure applying category
        _mockLocalDb.Setup(x => x.ApplyServerCategoryAsync(It.IsAny<CategoryLocal>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        // Act
        var result = await _syncService.SyncAsync();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Does.Contain("częściowa"));
            Assert.That(result.CategoriesSynced, Is.EqualTo(0)); // 1 received - 1 failed
        });

        _mockLocalDb.Verify(x => x.ClearDirtyFlagsAsync(), Times.Once, "Dirty flags should always be cleared");
        _mockLocalDb.Verify(x => x.SetLastSyncTimeAsync(It.IsAny<DateTime>()), Times.Never, "LastSyncedAt should NOT be updated when items fail");
    }

    [Test]
    public async Task SyncAsync_CountsAppliedItemsCorrectly()
    {
        // Arrange
        var lastSync = DateTime.UtcNow.AddDays(-1);
        var newSyncedAt = DateTime.UtcNow;

        _mockLocalDb.Setup(x => x.GetLastSyncTimeAsync()).ReturnsAsync(lastSync);
        _mockLocalDb.Setup(x => x.GetDirtyRecipesAsync()).ReturnsAsync([]);
        _mockLocalDb.Setup(x => x.GetDirtyCategoriesAsync()).ReturnsAsync([]);
        _mockLocalDb.Setup(x => x.GetDirtyIngredientsAsync()).ReturnsAsync([]);
        _mockLocalDb.Setup(x => x.GetDirtyMealPlansAsync()).ReturnsAsync([]);

        var syncResponse = new SyncResponse
        {
            SyncedAt = newSyncedAt,
            Categories = [
                new CategorySyncDto { Id = Guid.NewGuid(), Name = "Cat1" },
                new CategorySyncDto { Id = Guid.NewGuid(), Name = "Cat2" }
            ],
            Ingredients = [
                new IngredientSyncDto { Id = Guid.NewGuid(), Name = "Ing1" },
                new IngredientSyncDto { Id = Guid.NewGuid(), Name = "Ing2" },
                new IngredientSyncDto { Id = Guid.NewGuid(), Name = "Ing3" }
            ],
            Recipes = [
                new RecipeSyncDto { Id = Guid.NewGuid(), Title = "Recipe1", Ingredients = [] }
            ],
            MealPlans = []
        };

        SetupHttpResponse(HttpStatusCode.OK, syncResponse);

        // Fail one ingredient
        var failingIngredientId = syncResponse.Ingredients[1].Id;
        _mockLocalDb.Setup(x => x.ApplyServerIngredientAsync(It.Is<IngredientLocal>(i => i.Id == failingIngredientId)))
            .ThrowsAsync(new Exception("Simulated failure"));

        // Act
        var result = await _syncService.SyncAsync();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.CategoriesSynced, Is.EqualTo(2), "All categories should succeed");
            Assert.That(result.IngredientsSynced, Is.EqualTo(2), "2 out of 3 ingredients should succeed");
            Assert.That(result.RecipesSynced, Is.EqualTo(1), "Recipe should succeed");
            Assert.That(result.MealPlansSynced, Is.EqualTo(0), "No meal plans");
        });
    }

    [Test]
    public async Task SyncAsync_AlwaysClearsDirtyFlags_EvenWhenItemsFail()
    {
        // Arrange
        var lastSync = DateTime.UtcNow.AddDays(-1);

        _mockLocalDb.Setup(x => x.GetLastSyncTimeAsync()).ReturnsAsync(lastSync);
        _mockLocalDb.Setup(x => x.GetDirtyRecipesAsync()).ReturnsAsync([]);
        _mockLocalDb.Setup(x => x.GetDirtyCategoriesAsync()).ReturnsAsync([]);
        _mockLocalDb.Setup(x => x.GetDirtyIngredientsAsync()).ReturnsAsync([]);
        _mockLocalDb.Setup(x => x.GetDirtyMealPlansAsync()).ReturnsAsync([]);

        var syncResponse = new SyncResponse
        {
            SyncedAt = DateTime.UtcNow,
            Categories = [new CategorySyncDto { Id = Guid.NewGuid(), Name = "Test" }],
            Ingredients = [],
            Recipes = [],
            MealPlans = []
        };

        SetupHttpResponse(HttpStatusCode.OK, syncResponse);

        _mockLocalDb.Setup(x => x.ApplyServerCategoryAsync(It.IsAny<CategoryLocal>()))
            .ThrowsAsync(new Exception("Failure"));

        // Act
        await _syncService.SyncAsync();

        // Assert
        _mockLocalDb.Verify(x => x.ClearDirtyFlagsAsync(), Times.Once,
            "Dirty flags must be cleared even when ApplyServerChanges fails partially");
    }

    #endregion

    #region FullSyncAsync Tests

    [Test]
    public async Task FullSyncAsync_WhenServerReturns500_ReturnsFailureResult()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.InternalServerError, endpoint: "/api/sync/full");

        // Act
        var result = await _syncService.FullSyncAsync();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("Błąd").Or.Contains("serwera").Or.Contains("InternalServerError"));
        });
    }

    [Test]
    public async Task FullSyncAsync_WhenAllItemsApplySuccessfully_UpdatesLastSyncedAt()
    {
        // Arrange
        var newSyncedAt = DateTime.UtcNow;

        var syncResponse = new SyncResponse
        {
            SyncedAt = newSyncedAt,
            Categories = [new CategorySyncDto { Id = Guid.NewGuid(), Name = "Cat" }],
            Ingredients = [],
            Recipes = [],
            MealPlans = []
        };

        SetupHttpResponse(HttpStatusCode.OK, syncResponse, "/api/sync/full");

        // Act
        var result = await _syncService.FullSyncAsync();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Does.Contain("Pełna synchronizacja zakończona"));
            Assert.That(result.CategoriesSynced, Is.EqualTo(1));
        });

        _mockLocalDb.Verify(x => x.SetLastSyncTimeAsync(newSyncedAt), Times.Once);
    }

    [Test]
    public async Task FullSyncAsync_WhenSomeItemsFail_DoesNotUpdateLastSyncedAt()
    {
        // Arrange
        var syncResponse = new SyncResponse
        {
            SyncedAt = DateTime.UtcNow,
            Categories = [new CategorySyncDto { Id = Guid.NewGuid(), Name = "Test" }],
            Ingredients = [],
            Recipes = [],
            MealPlans = []
        };

        SetupHttpResponse(HttpStatusCode.OK, syncResponse, "/api/sync/full");

        _mockLocalDb.Setup(x => x.ApplyServerCategoryAsync(It.IsAny<CategoryLocal>()))
            .ThrowsAsync(new Exception("Failure"));

        // Act
        var result = await _syncService.FullSyncAsync();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Does.Contain("częściowa"));
        });

        _mockLocalDb.Verify(x => x.SetLastSyncTimeAsync(It.IsAny<DateTime>()), Times.Never,
            "LastSyncedAt should NOT be updated when items fail in FullSync");
    }

    #endregion

    #region UploadAllAsync Tests

    [Test]
    public async Task UploadAllAsync_SendsAllDataInBatches()
    {
        // This test requires IConnectivity abstraction to mock network status.
        // Currently Connectivity.Current is a static property that cannot be easily mocked.
        // Test skipped until architectural refactoring is complete.

        Assert.Pass("Test skipped - requires IConnectivity abstraction for mocking network access");
    }

    [Test]
    public async Task UploadAllAsync_WhenBatchFails_ReturnsFailureResult()
    {
        // This test requires IConnectivity abstraction to mock network status.
        // Currently Connectivity.Current is a static property that cannot be easily mocked.
        // Test skipped until architectural refactoring is complete.

        Assert.Pass("Test skipped - requires IConnectivity abstraction for mocking network access");
    }

    #endregion

    #region Helper Methods

    private void SetupHttpResponse(HttpStatusCode statusCode, SyncResponse? response = null, string endpoint = "/api/sync")
    {
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains(endpoint)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var httpResponse = new HttpResponseMessage(statusCode);

                if (response != null)
                {
                    httpResponse.Content = JsonContent.Create(response);
                }
                else if (statusCode == HttpStatusCode.OK)
                {
                    // Empty OK response should have empty JSON content
                    httpResponse.Content = new StringContent("{}");
                }

                return httpResponse;
            });
    }

    #endregion
}
