using System.Net;
using System.Net.Http.Json;
using ReceptyOks.Shared.Models;

namespace ReceptyOks.Api.IntegrationTests;

[TestFixture]
public class RecipeEndpointsTests
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
    public async Task GetAllRecipes_WhenNoRecipesExist_ReturnsEmptyList()
    {
        // Act
        var response = await _client.GetAsync("/api/recipes");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var recipes = await response.Content.ReadFromJsonAsync<List<Recipe>>();
        Assert.That(recipes, Is.Not.Null);
        Assert.That(recipes, Is.Empty);
    }

    [Test]
    public async Task GetAllRecipes_WhenUnauthorized_Returns401()
    {
        // Arrange - use client without API key
        using var unauthenticatedClient = _factory.CreateClient();

        // Act
        var response = await unauthenticatedClient.GetAsync("/api/recipes");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task CreateRecipe_WithValidData_ReturnsCreated()
    {
        // Arrange
        var newRecipe = new Recipe
        {
            Title = "Test Recipe",
            Description = "Test Description",
            Instructions = "Step 1, Step 2",
            PreparationTimeMinutes = 10,
            CookingTimeMinutes = 20,
            Servings = 4
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/recipes", newRecipe);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var createdRecipe = await response.Content.ReadFromJsonAsync<Recipe>();
        Assert.That(createdRecipe, Is.Not.Null);
        Assert.That(createdRecipe!.Title, Is.EqualTo("Test Recipe"));
        Assert.That(createdRecipe.Id, Is.Not.EqualTo(Guid.Empty));
    }

    [Test]
    public async Task GetRecipeById_WhenRecipeExists_ReturnsRecipe()
    {
        // Arrange - create a recipe first
        var newRecipe = new Recipe
        {
            Title = "Get By Id Test",
            Description = "Description",
            Instructions = "Instructions"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/recipes", newRecipe);
        var createdRecipe = await createResponse.Content.ReadFromJsonAsync<Recipe>();

        // Act
        var response = await _client.GetAsync($"/api/recipes/{createdRecipe!.Id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var recipe = await response.Content.ReadFromJsonAsync<Recipe>();
        Assert.That(recipe, Is.Not.Null);
        Assert.That(recipe!.Title, Is.EqualTo("Get By Id Test"));
    }

    [Test]
    public async Task GetRecipeById_WhenRecipeDoesNotExist_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/recipes/{Guid.NewGuid()}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task UpdateRecipe_WhenRecipeExists_ReturnsUpdatedRecipe()
    {
        // Arrange
        var newRecipe = new Recipe
        {
            Title = "Original Title",
            Description = "Original Description",
            Instructions = "Original Instructions"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/recipes", newRecipe);
        var createdRecipe = await createResponse.Content.ReadFromJsonAsync<Recipe>();

        var updatedRecipe = new Recipe
        {
            Title = "Updated Title",
            Description = "Updated Description",
            Instructions = "Updated Instructions",
            PreparationTimeMinutes = 15,
            CookingTimeMinutes = 30,
            Servings = 6
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/recipes/{createdRecipe!.Id}", updatedRecipe);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<Recipe>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Title, Is.EqualTo("Updated Title"));
        Assert.That(result.PreparationTimeMinutes, Is.EqualTo(15));
    }

    [Test]
    public async Task UpdateRecipe_WhenRecipeDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var updatedRecipe = new Recipe { Title = "Test" };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/recipes/{Guid.NewGuid()}", updatedRecipe);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task DeleteRecipe_WhenRecipeExists_ReturnsNoContent()
    {
        // Arrange
        var newRecipe = new Recipe
        {
            Title = "Recipe To Delete",
            Description = "Will be deleted"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/recipes", newRecipe);
        var createdRecipe = await createResponse.Content.ReadFromJsonAsync<Recipe>();

        // Act
        var deleteResponse = await _client.DeleteAsync($"/api/recipes/{createdRecipe!.Id}");

        // Assert
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // Verify soft delete - recipe should not appear in list
        var getAllResponse = await _client.GetAsync("/api/recipes");
        var recipes = await getAllResponse.Content.ReadFromJsonAsync<List<Recipe>>();
        Assert.That(recipes!.Any(r => r.Id == createdRecipe.Id), Is.False);
    }

    [Test]
    public async Task DeleteRecipe_WhenRecipeDoesNotExist_ReturnsNotFound()
    {
        // Act
        var response = await _client.DeleteAsync($"/api/recipes/{Guid.NewGuid()}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task SearchRecipes_WhenMatchingRecipesExist_ReturnsFilteredList()
    {
        // Arrange
        var recipe1 = new Recipe { Title = "Chocolate Cake", Description = "Sweet dessert" };
        var recipe2 = new Recipe { Title = "Vanilla Ice Cream", Description = "Cold treat" };
        var recipe3 = new Recipe { Title = "Strawberry Cake", Description = "Berry dessert" };

        await _client.PostAsJsonAsync("/api/recipes", recipe1);
        await _client.PostAsJsonAsync("/api/recipes", recipe2);
        await _client.PostAsJsonAsync("/api/recipes", recipe3);

        // Act
        var response = await _client.GetAsync("/api/recipes/search?query=Cake");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var recipes = await response.Content.ReadFromJsonAsync<List<Recipe>>();
        Assert.That(recipes, Is.Not.Null);
        Assert.That(recipes!.Count, Is.EqualTo(2));
        Assert.That(recipes.All(r => r.Title.Contains("Cake")), Is.True);
    }

    [Test]
    public async Task SearchRecipes_WhenNoMatchingRecipes_ReturnsEmptyList()
    {
        // Arrange
        var recipe = new Recipe { Title = "Pizza", Description = "Italian food" };
        await _client.PostAsJsonAsync("/api/recipes", recipe);

        // Act
        var response = await _client.GetAsync("/api/recipes/search?query=Sushi");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var recipes = await response.Content.ReadFromJsonAsync<List<Recipe>>();
        Assert.That(recipes, Is.Not.Null);
        Assert.That(recipes, Is.Empty);
    }
}
