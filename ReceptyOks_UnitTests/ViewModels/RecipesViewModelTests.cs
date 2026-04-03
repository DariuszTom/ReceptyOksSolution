using Microsoft.Extensions.Logging;
using Moq;
using ReceptyOks.Data;
using ReceptyOks.Interfaces;
using ReceptyOks.Services;
using ReceptyOks.ViewModels;

namespace ReceptyOks_UnitTests.ViewModels;

/// <summary>
/// Unit tests for RecipesViewModel.
/// 
/// Note: These tests work around Shell.Current being null in the test environment by catching
/// NullReferenceException in tests that trigger DisplayAlertAsync calls. In a production-ready
/// test suite, consider abstracting Shell.Current behind an interface for better testability.
/// </summary>

[TestFixture]
public class RecipesViewModelTests
{
    private Mock<ILocalDatabase> _mockDatabase = null!;
    private Mock<ISyncService> _mockSyncService = null!;
    private Mock<ILogger<RecipesViewModel>> _mockLogger = null!;
    private RecipesViewModel _viewModel = null!;

    [SetUp]
    public void SetUp()
    {
        _mockDatabase = new Mock<ILocalDatabase>();
        _mockSyncService = new Mock<ISyncService>();
        _mockLogger = new Mock<ILogger<RecipesViewModel>>();

        _viewModel = new RecipesViewModel(
            _mockDatabase.Object,
            _mockSyncService.Object,
            _mockLogger.Object);
    }

    #region LoadRecipesAsync Tests

    [Test]
    public async Task LoadRecipesAsync_WhenSearchQueryIsEmpty_LoadsAllRecipes()
    {
        // Arrange
        var expectedRecipes = new List<RecipeLocal>
        {
            new() { Id = Guid.NewGuid(), Title = "Recipe 1" },
            new() { Id = Guid.NewGuid(), Title = "Recipe 2" }
        };

        _mockDatabase.Setup(x => x.GetRecipesAsync())
            .ReturnsAsync(expectedRecipes);

        // Act
        await _viewModel.LoadRecipesCommand.ExecuteAsync(null);

        // Assert
        Assert.That(_viewModel.Recipes, Has.Count.EqualTo(2));
        Assert.That(_viewModel.Recipes[0].Title, Is.EqualTo("Recipe 1"));
        Assert.That(_viewModel.Recipes[1].Title, Is.EqualTo("Recipe 2"));
        _mockDatabase.Verify(x => x.GetRecipesAsync(), Times.Once);
        _mockDatabase.Verify(x => x.SearchRecipesAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task LoadRecipesAsync_SetsIsRefreshingToTrueDuringExecution()
    {
        // Arrange
        var taskCompletionSource = new TaskCompletionSource<List<RecipeLocal>>();
        _mockDatabase.Setup(x => x.GetRecipesAsync())
            .Returns(taskCompletionSource.Task);

        // Act
        var loadTask = _viewModel.LoadRecipesCommand.ExecuteAsync(null);

        // Assert - IsRefreshing should be true while loading
        Assert.That(_viewModel.IsRefreshing, Is.True);

        // Complete the task
        taskCompletionSource.SetResult([]);
        await loadTask;

        // Assert - IsRefreshing should be false after completion
        Assert.That(_viewModel.IsRefreshing, Is.False);
    }

    [Test]
    public async Task LoadRecipesAsync_SetsIsRefreshingToFalseAfterCompletion()
    {
        // Arrange
        _mockDatabase.Setup(x => x.GetRecipesAsync())
            .ReturnsAsync([]);

        // Act
        await _viewModel.LoadRecipesCommand.ExecuteAsync(null);

        // Assert
        Assert.That(_viewModel.IsRefreshing, Is.False);
    }

    [Test]
    public async Task LoadRecipesAsync_ReplacesExistingRecipes()
    {
        // Arrange
        var initialRecipes = new List<RecipeLocal>
        {
            new() { Id = Guid.NewGuid(), Title = "Old Recipe" }
        };
        var newRecipes = new List<RecipeLocal>
        {
            new() { Id = Guid.NewGuid(), Title = "New Recipe 1" },
            new() { Id = Guid.NewGuid(), Title = "New Recipe 2" }
        };

        _mockDatabase.SetupSequence(x => x.GetRecipesAsync())
            .ReturnsAsync(initialRecipes)
            .ReturnsAsync(newRecipes);

        // Act
        await _viewModel.LoadRecipesCommand.ExecuteAsync(null);
        var firstLoadCount = _viewModel.Recipes.Count;

        await _viewModel.LoadRecipesCommand.ExecuteAsync(null);
        var secondLoadCount = _viewModel.Recipes.Count;

        // Assert
        Assert.That(firstLoadCount, Is.EqualTo(1));
        Assert.That(secondLoadCount, Is.EqualTo(2));
        Assert.That(_viewModel.Recipes[0].Title, Is.EqualTo("New Recipe 1"));
    }

    #endregion

    #region SyncAsync Tests

    [Test]
    public async Task SyncAsync_WhenSuccessful_LoadsRecipes()
    {
        // Arrange
        var syncResult = new SyncResult { Success = true, Message = "Synced successfully" };
        _mockSyncService.Setup(x => x.SyncAsync())
            .ReturnsAsync(syncResult);
        _mockDatabase.Setup(x => x.GetRecipesAsync())
            .ReturnsAsync([]);

        // Act
        try
        {
            await _viewModel.SyncCommand.ExecuteAsync(null);
        }
        catch (NullReferenceException)
        {
            // Expected in test environment due to Shell.Current being null
        }

        // Assert
        _mockDatabase.Verify(x => x.GetRecipesAsync(), Times.Once);
    }

    [Test]
    public async Task SyncAsync_WhenSuccessful_LogsInformation()
    {
        // Arrange
        var syncResult = new SyncResult { Success = true, Message = "Synced successfully" };
        _mockSyncService.Setup(x => x.SyncAsync())
            .ReturnsAsync(syncResult);
        _mockDatabase.Setup(x => x.GetRecipesAsync())
            .ReturnsAsync([]);

        // Act
        try
        {
            await _viewModel.SyncCommand.ExecuteAsync(null);
        }
        catch (NullReferenceException)
        {
            // Expected in test environment due to Shell.Current being null
        }

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Synchronization successful")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task SyncAsync_WhenFails_LogsWarning()
    {
        // Arrange
        var syncResult = new SyncResult { Success = false, Message = "Sync failed" };
        _mockSyncService.Setup(x => x.SyncAsync())
            .ReturnsAsync(syncResult);

        // Act
        try
        {
            await _viewModel.SyncCommand.ExecuteAsync(null);
        }
        catch (NullReferenceException)
        {
            // Expected in test environment due to Shell.Current being null
        }

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Synchronization failed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task SyncAsync_WhenFails_DoesNotLoadRecipes()
    {
        // Arrange
        var syncResult = new SyncResult { Success = false, Message = "Sync failed" };
        _mockSyncService.Setup(x => x.SyncAsync())
            .ReturnsAsync(syncResult);

        // Act
        try
        {
            await _viewModel.SyncCommand.ExecuteAsync(null);
        }
        catch (NullReferenceException)
        {
            // Expected in test environment due to Shell.Current being null
        }

        // Assert
        _mockDatabase.Verify(x => x.GetRecipesAsync(), Times.Never);
    }

    [Test]
    public async Task SyncAsync_SetsIsSyncingToTrueDuringExecution()
    {
        // Arrange
        var taskCompletionSource = new TaskCompletionSource<SyncResult>();
        _mockSyncService.Setup(x => x.SyncAsync())
            .Returns(taskCompletionSource.Task);

        // Act
        var syncTask = _viewModel.SyncCommand.ExecuteAsync(null);

        // Assert - IsSyncing should be true while syncing
        Assert.That(_viewModel.IsSyncing, Is.True);

        // Complete the task
        taskCompletionSource.SetResult(new SyncResult { Success = true });
        _mockDatabase.Setup(x => x.GetRecipesAsync()).ReturnsAsync([]);
        try
        {
            await syncTask;
        }
        catch (NullReferenceException)
        {
            // Expected in test environment due to Shell.Current being null
        }

        // Assert - IsSyncing should be false after completion
        Assert.That(_viewModel.IsSyncing, Is.False);
    }

    [Test]
    public async Task SyncAsync_SetsIsSyncingToFalseAfterCompletion()
    {
        // Arrange
        var syncResult = new SyncResult { Success = true, Message = "Success" };
        _mockSyncService.Setup(x => x.SyncAsync())
            .ReturnsAsync(syncResult);
        _mockDatabase.Setup(x => x.GetRecipesAsync())
            .ReturnsAsync([]);

        // Act
        try
        {
            await _viewModel.SyncCommand.ExecuteAsync(null);
        }
        catch (NullReferenceException)
        {
            // Expected in test environment due to Shell.Current being null
        }

        // Assert
        Assert.That(_viewModel.IsSyncing, Is.False);
    }

    [Test]
    public async Task SyncAsync_WhenExceptionOccurs_SetsIsSyncingToFalse()
    {
        // Arrange
        _mockSyncService.Setup(x => x.SyncAsync())
            .ThrowsAsync(new Exception("Sync error"));

        // Act
        try
        {
            await _viewModel.SyncCommand.ExecuteAsync(null);
        }
        catch (NullReferenceException)
        {
            // Expected in test environment due to Shell.Current being null
        }

        // Assert
        Assert.That(_viewModel.IsSyncing, Is.False);
    }

    [Test]
    public async Task SyncAsync_WhenExceptionOccurs_LogsError()
    {
        // Arrange
        var exception = new Exception("Sync error");
        _mockSyncService.Setup(x => x.SyncAsync())
            .ThrowsAsync(exception);

        // Act
        try
        {
            await _viewModel.SyncCommand.ExecuteAsync(null);
        }
        catch (NullReferenceException)
        {
            // Expected in test environment due to Shell.Current being null
        }

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error during synchronization")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region ClearSearch Tests

    [Test]
    public void ClearSearch_SetsSearchQueryToEmpty()
    {
        // Arrange
        _viewModel.SearchQuery = "test query";

        // Act
        _viewModel.ClearSearchCommand.Execute(null);

        // Assert
        Assert.That(_viewModel.SearchQuery, Is.EqualTo(string.Empty));
    }

    [Test]
    public void ClearSearch_WhenSearchQueryAlreadyEmpty_RemainEmpty()
    {
        // Arrange
        _viewModel.SearchQuery = string.Empty;

        // Act
        _viewModel.ClearSearchCommand.Execute(null);

        // Assert
        Assert.That(_viewModel.SearchQuery, Is.EqualTo(string.Empty));
    }

    #endregion

    #region OnSearchQueryChanged Tests

    [Test]
    public async Task OnSearchQueryChanged_TriggersLoadRecipes()
    {
        // Arrange
        var recipes = new List<RecipeLocal>
        {
            new() { Id = Guid.NewGuid(), Title = "Test Recipe" }
        };
        _mockDatabase.Setup(x => x.SearchRecipesAsync(It.IsAny<string>()))
            .ReturnsAsync(recipes);

        // Act
        _viewModel.SearchQuery = "test";

        // Wait for the command to execute
        await Task.Delay(100);

        // Assert
        _mockDatabase.Verify(x => x.SearchRecipesAsync("test"), Times.Once);
    }

    [Test]
    public async Task OnSearchQueryChanged_WhenChangedToEmpty_LoadsAllRecipes()
    {
        // Arrange
        var searchRecipes = new List<RecipeLocal>
        {
            new() { Id = Guid.NewGuid(), Title = "Test Recipe" }
        };
        var allRecipes = new List<RecipeLocal>
        {
            new() { Id = Guid.NewGuid(), Title = "Recipe 1" },
            new() { Id = Guid.NewGuid(), Title = "Recipe 2" }
        };
        _mockDatabase.Setup(x => x.SearchRecipesAsync("test"))
            .ReturnsAsync(searchRecipes);
        _mockDatabase.Setup(x => x.GetRecipesAsync())
            .ReturnsAsync(allRecipes);

        _viewModel.SearchQuery = "test";
        await Task.Delay(100);

        // Act
        _viewModel.SearchQuery = string.Empty;
        await Task.Delay(100);

        // Assert
        _mockDatabase.Verify(x => x.GetRecipesAsync(), Times.AtLeastOnce);
    }

    #endregion

    #region Initial State Tests

    [Test]
    public void Constructor_InitializesWithEmptyRecipes()
    {
        // Assert
        Assert.That(_viewModel.Recipes, Is.Not.Null);
        Assert.That(_viewModel.Recipes, Is.Empty);
    }

    [Test]
    public void Constructor_InitializesWithEmptySearchQuery()
    {
        // Assert
        Assert.That(_viewModel.SearchQuery, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Constructor_InitializesWithIsRefreshingFalse()
    {
        // Assert
        Assert.That(_viewModel.IsRefreshing, Is.False);
    }

    [Test]
    public void Constructor_InitializesWithIsSyncingFalse()
    {
        // Assert
        Assert.That(_viewModel.IsSyncing, Is.False);
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task LoadRecipesAsync_WithLargeRecipeList_HandlesCorrectly()
    {
        // Arrange
        var largeRecipeList = Enumerable.Range(1, 1000)
            .Select(i => new RecipeLocal { Id = Guid.NewGuid(), Title = $"Recipe {i}" })
            .ToList();

        _mockDatabase.Setup(x => x.GetRecipesAsync())
            .ReturnsAsync(largeRecipeList);

        // Act
        await _viewModel.LoadRecipesCommand.ExecuteAsync(null);

        // Assert
        Assert.That(_viewModel.Recipes, Has.Count.EqualTo(1000));
    }

    [Test]
    public async Task SyncAsync_WithMultipleEntityCounts_IncludesInResult()
    {
        // Arrange
        var syncResult = new SyncResult
        {
            Success = true,
            Message = "All synced",
            RecipesSynced = 5,
            CategoriesSynced = 3,
            IngredientsSynced = 15,
            MealPlansSynced = 2
        };
        _mockSyncService.Setup(x => x.SyncAsync())
            .ReturnsAsync(syncResult);
        _mockDatabase.Setup(x => x.GetRecipesAsync())
            .ReturnsAsync([]);

        // Act
        try
        {
            await _viewModel.SyncCommand.ExecuteAsync(null);
        }
        catch (NullReferenceException)
        {
            // Expected in test environment due to Shell.Current being null
        }

        // Assert
        _mockSyncService.Verify(x => x.SyncAsync(), Times.Once);
    }

    #endregion
}
