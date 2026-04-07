using Moq;
using ReceptyOks.Data;
using ReceptyOks.Shared.Misc;
using ReceptyOks.ViewModels;

namespace ReceptyOks_UnitTests.ViewModels;

[TestFixture]
public class IngredientsCalculationViewModelTests
{
    private Mock<ILocalDatabase> _mockDatabase = null!;
    private IngredientsCalculationViewModel _viewModel = null!;

    [SetUp]
    public void SetUp()
    {
        _mockDatabase = new Mock<ILocalDatabase>();
        _viewModel = new IngredientsCalculationViewModel(_mockDatabase.Object);
    }

    #region LoadRecipesAsync Tests

    [Test]
    public async Task LoadRecipesAsync_WhenSearchQueryIsEmpty_LoadsAllRecipeSummaries()
    {
        // Arrange
        var expectedSummaries = new List<RecipeSummary>
        {
            new(Guid.NewGuid(), "Ciasto czekoladowe"),
            new(Guid.NewGuid(), "Sernik")
        };

        _mockDatabase.Setup(x => x.GetRecipeSummariesAsync(null))
            .ReturnsAsync(expectedSummaries);

        // Act
        await _viewModel.LoadRecipesCommand.ExecuteAsync(null);

        // Assert
        Assert.That(_viewModel.Recipes, Has.Count.EqualTo(2));
        Assert.That(_viewModel.Recipes[0].Title, Is.EqualTo("Ciasto czekoladowe"));
        _mockDatabase.Verify(x => x.GetRecipeSummariesAsync(null), Times.Once);
    }

    [Test]
    public async Task LoadRecipesAsync_WhenSearchQueryHasValue_PassesQueryToDatabase()
    {
        // Arrange
        _viewModel.SearchQuery = "ciasto";
        var expectedSummaries = new List<RecipeSummary>
        {
            new(Guid.NewGuid(), "Ciasto czekoladowe")
        };

        _mockDatabase.Setup(x => x.GetRecipeSummariesAsync("ciasto"))
            .ReturnsAsync(expectedSummaries);

        // Act
        await _viewModel.LoadRecipesCommand.ExecuteAsync(null);

        // Assert
        Assert.That(_viewModel.Recipes, Has.Count.EqualTo(1));
        _mockDatabase.Verify(x => x.GetRecipeSummariesAsync("ciasto"), Times.Once);
    }

    [Test]
    public async Task LoadRecipesAsync_SetsIsLoadingDuringExecution()
    {
        // Arrange
        var taskCompletionSource = new TaskCompletionSource<List<RecipeSummary>>();
        _mockDatabase.Setup(x => x.GetRecipeSummariesAsync(null))
            .Returns(taskCompletionSource.Task);

        // Act
        var loadTask = _viewModel.LoadRecipesCommand.ExecuteAsync(null);

        // Assert - IsLoading should be true while loading
        Assert.That(_viewModel.IsLoading, Is.True);

        // Complete the task
        taskCompletionSource.SetResult([]);
        await loadTask;

        // Assert - IsLoading should be false after completion
        Assert.That(_viewModel.IsLoading, Is.False);
    }

    #endregion

    #region LoadIngredientsAsync Tests

    [Test]
    public async Task LoadIngredientsAsync_LoadsAndMapsIngredientsCorrectly()
    {
        // Arrange
        var recipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var recipeIngredients = new List<RecipeIngredientLocal>
        {
            new() { Id = Guid.NewGuid(), RecipeId = recipeId, IngredientId = ingredientId, Quantity = 200, Unit = "g", Order = 1 }
        };

        var ingredients = new List<IngredientLocal>
        {
            new() { Id = ingredientId, Name = "Mąka" }
        };

        _mockDatabase.Setup(x => x.GetRecipeIngredientsAsync(recipeId))
            .ReturnsAsync(recipeIngredients);
        _mockDatabase.Setup(x => x.GetIngredientsAsync())
            .ReturnsAsync(ingredients);

        // Act
        await _viewModel.LoadIngredientsCommand.ExecuteAsync(recipeId);

        // Assert
        Assert.That(_viewModel.Ingredients, Has.Count.EqualTo(1));
        Assert.That(_viewModel.Ingredients[0].Name, Is.EqualTo("Mąka"));
        Assert.That(_viewModel.Ingredients[0].Quantity, Is.EqualTo(200));
        Assert.That(_viewModel.Ingredients[0].Unit, Is.EqualTo("g"));
    }

    [Test]
    public async Task LoadIngredientsAsync_WhenIngredientNotFound_UsesDefaultName()
    {
        // Arrange
        var recipeId = Guid.NewGuid();
        var unknownIngredientId = Guid.NewGuid();

        var recipeIngredients = new List<RecipeIngredientLocal>
        {
            new() { Id = Guid.NewGuid(), RecipeId = recipeId, IngredientId = unknownIngredientId, Quantity = 100, Order = 1 }
        };

        _mockDatabase.Setup(x => x.GetRecipeIngredientsAsync(recipeId))
            .ReturnsAsync(recipeIngredients);
        _mockDatabase.Setup(x => x.GetIngredientsAsync())
            .ReturnsAsync(new List<IngredientLocal>());

        // Act
        await _viewModel.LoadIngredientsCommand.ExecuteAsync(recipeId);

        // Assert
        Assert.That(_viewModel.Ingredients[0].Name, Is.EqualTo("Nieznany"));
    }

    [Test]
    public async Task LoadIngredientsAsync_ClearsScaledIngredients()
    {
        // Arrange
        var recipeId = Guid.NewGuid();
        _viewModel.ScaledIngredients.Add(new ScaledIngredient("Test", 100, 150, "g"));

        _mockDatabase.Setup(x => x.GetRecipeIngredientsAsync(recipeId))
            .ReturnsAsync(new List<RecipeIngredientLocal>());
        _mockDatabase.Setup(x => x.GetIngredientsAsync())
            .ReturnsAsync(new List<IngredientLocal>());

        // Act
        await _viewModel.LoadIngredientsCommand.ExecuteAsync(recipeId);

        // Assert
        Assert.That(_viewModel.ScaledIngredients, Is.Empty);
    }

    [Test]
    public async Task LoadIngredientsAsync_OrdersIngredientsByOrder()
    {
        // Arrange
        var recipeId = Guid.NewGuid();
        var ingredientId1 = Guid.NewGuid();
        var ingredientId2 = Guid.NewGuid();

        var recipeIngredients = new List<RecipeIngredientLocal>
        {
            new() { Id = Guid.NewGuid(), RecipeId = recipeId, IngredientId = ingredientId2, Quantity = 100, Order = 2 },
            new() { Id = Guid.NewGuid(), RecipeId = recipeId, IngredientId = ingredientId1, Quantity = 200, Order = 1 }
        };

        var ingredients = new List<IngredientLocal>
        {
            new() { Id = ingredientId1, Name = "First" },
            new() { Id = ingredientId2, Name = "Second" }
        };

        _mockDatabase.Setup(x => x.GetRecipeIngredientsAsync(recipeId))
            .ReturnsAsync(recipeIngredients);
        _mockDatabase.Setup(x => x.GetIngredientsAsync())
            .ReturnsAsync(ingredients);

        // Act
        await _viewModel.LoadIngredientsCommand.ExecuteAsync(recipeId);

        // Assert
        Assert.That(_viewModel.Ingredients[0].Name, Is.EqualTo("First"));
        Assert.That(_viewModel.Ingredients[1].Name, Is.EqualTo("Second"));
    }

    #endregion

    #region CalculateScaledIngredients Tests

    [Test]
    public void CalculateScaledIngredients_WhenNoIngredients_DoesNothing()
    {
        // Arrange - Ingredients is empty by default

        // Act
        _viewModel.CalculateScaledIngredientsCommand.Execute(null);

        // Assert
        Assert.That(_viewModel.ScaledIngredients, Is.Empty);
        Assert.That(_viewModel.ScalingMultiplier, Is.EqualTo(1));
    }

    [Test]
    public void CalculateScaledIngredients_CalculatesCorrectMultiplier()
    {
        // Arrange
        _viewModel.Ingredients.Add(new RecipeIngredientDisplay { Name = "Mąka", Quantity = 200, Unit = "g" });
        _viewModel.OriginalForm = BakingForm.Circular(20);
        _viewModel.NewForm = BakingForm.Circular(24);

        // Act
        _viewModel.CalculateScaledIngredientsCommand.Execute(null);

        // Assert - 24cm vs 20cm should give multiplier of (24/20)^2 = 1.44
        var expectedMultiplier = (decimal)(Math.PI * 144) / (decimal)(Math.PI * 100); // 1.44
        Assert.That(_viewModel.ScalingMultiplier, Is.EqualTo(expectedMultiplier).Within(0.01m));
    }

    [Test]
    public void CalculateScaledIngredients_ScalesIngredientQuantities()
    {
        // Arrange
        _viewModel.Ingredients.Add(new RecipeIngredientDisplay { Name = "Mąka", Quantity = 200, Unit = "g" });
        _viewModel.OriginalForm = BakingForm.Circular(20);
        _viewModel.NewForm = BakingForm.Circular(24);

        // Act
        _viewModel.CalculateScaledIngredientsCommand.Execute(null);

        // Assert
        Assert.That(_viewModel.ScaledIngredients, Has.Count.EqualTo(1));
        Assert.That(_viewModel.ScaledIngredients[0].Name, Is.EqualTo("Mąka"));
        Assert.That(_viewModel.ScaledIngredients[0].OriginalQuantity, Is.EqualTo(200));
        Assert.That(_viewModel.ScaledIngredients[0].Quantity, Is.GreaterThan(200)); // Scaled up
    }

    [Test]
    public void CalculateScaledIngredients_WithRectangularForms_CalculatesCorrectly()
    {
        // Arrange
        _viewModel.Ingredients.Add(new RecipeIngredientDisplay { Name = "Cukier", Quantity = 100, Unit = "g" });
        _viewModel.OriginalForm = BakingForm.Rectangular(20, 30); // 600 cm²
        _viewModel.NewForm = BakingForm.Rectangular(25, 35);       // 875 cm²

        // Act
        _viewModel.CalculateScaledIngredientsCommand.Execute(null);

        // Assert
        var expectedMultiplier = 875m / 600m;
        Assert.That(_viewModel.ScalingMultiplier, Is.EqualTo(expectedMultiplier).Within(0.01m));
    }

    #endregion

    #region ClearSelection Tests

    [Test]
    public void ClearSelection_ResetsAllState()
    {
        // Arrange
        _viewModel.SelectedRecipe = new RecipeSummary(Guid.NewGuid(), "Test");
        _viewModel.SearchQuery = "search";
        _viewModel.Ingredients.Add(new RecipeIngredientDisplay { Name = "Test", Quantity = 100, Unit = "g" });
        _viewModel.ScaledIngredients.Add(new ScaledIngredient("Test", 100, 150, "g"));
        _viewModel.ScalingMultiplier = 1.5m;

        // Act
        _viewModel.ClearSelectionCommand.Execute(null);

        // Assert
        Assert.That(_viewModel.SelectedRecipe, Is.Null);
        Assert.That(_viewModel.SearchQuery, Is.Empty);
        Assert.That(_viewModel.Ingredients, Is.Empty);
        Assert.That(_viewModel.ScaledIngredients, Is.Empty);
        Assert.That(_viewModel.ScalingMultiplier, Is.EqualTo(1));
    }

    #endregion

    #region Default Values Tests

    [Test]
    public void Constructor_SetsDefaultFormValues()
    {
        // Assert
        Assert.That(_viewModel.OriginalForm.Shape, Is.EqualTo(FormShape.Circular));
        Assert.That(_viewModel.OriginalForm.Diameter, Is.EqualTo(24));
        Assert.That(_viewModel.NewForm.Shape, Is.EqualTo(FormShape.Circular));
        Assert.That(_viewModel.NewForm.Diameter, Is.EqualTo(26));
    }

    [Test]
    public void Constructor_InitializesEmptyCollections()
    {
        // Assert
        Assert.That(_viewModel.Recipes, Is.Empty);
        Assert.That(_viewModel.Ingredients, Is.Empty);
        Assert.That(_viewModel.ScaledIngredients, Is.Empty);
    }

    #endregion
}
