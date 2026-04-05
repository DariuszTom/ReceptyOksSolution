using ReceptyOks.Shared.Models;
using System.Net;
using System.Net.Http.Json;

namespace ReceptyOks.Api.IntegrationTests;

[TestFixture]
public class CategoryEndpointsTests
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
    public async Task GetAllCategories_WhenNoCategoriesExist_ReturnsEmptyList()
    {
        // Act
        var response = await _client.GetAsync("/api/categories");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var categories = await response.Content.ReadFromJsonAsync<List<Category>>();
        Assert.That(categories, Is.Not.Null);
        Assert.That(categories, Is.Empty);
    }

    [Test]
    public async Task GetAllCategories_WhenUnauthorized_Returns401()
    {
        // Arrange
        using var unauthenticatedClient = _factory.CreateClient();

        // Act
        var response = await unauthenticatedClient.GetAsync("/api/categories");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task CreateCategory_WithValidData_ReturnsCreated()
    {
        // Arrange
        var newCategory = new Category
        {
            Name = "Desserts",
            Description = "Sweet treats",
            IconName = "cake"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/categories", newCategory);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var createdCategory = await response.Content.ReadFromJsonAsync<Category>();
        Assert.That(createdCategory, Is.Not.Null);
        Assert.That(createdCategory!.Name, Is.EqualTo("Desserts"));
        Assert.That(createdCategory.Id, Is.Not.EqualTo(Guid.Empty));
    }

    [Test]
    public async Task GetCategoryById_WhenCategoryExists_ReturnsCategory()
    {
        // Arrange
        var newCategory = new Category
        {
            Name = "Main Dishes",
            Description = "Primary courses"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/categories", newCategory);
        var createdCategory = await createResponse.Content.ReadFromJsonAsync<Category>();

        // Act
        var response = await _client.GetAsync($"/api/categories/{createdCategory!.Id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var category = await response.Content.ReadFromJsonAsync<Category>();
        Assert.That(category, Is.Not.Null);
        Assert.That(category!.Name, Is.EqualTo("Main Dishes"));
    }

    [Test]
    public async Task GetCategoryById_WhenCategoryDoesNotExist_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/categories/{Guid.NewGuid()}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task UpdateCategory_WhenCategoryExists_ReturnsUpdatedCategory()
    {
        // Arrange
        var newCategory = new Category
        {
            Name = "Original Name",
            Description = "Original Description"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/categories", newCategory);
        var createdCategory = await createResponse.Content.ReadFromJsonAsync<Category>();

        var updatedCategory = new Category
        {
            Name = "Updated Name",
            Description = "Updated Description",
            IconName = "updated-icon"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/categories/{createdCategory!.Id}", updatedCategory);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<Category>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("Updated Name"));
        Assert.That(result.IconName, Is.EqualTo("updated-icon"));
    }

    [Test]
    public async Task UpdateCategory_WhenCategoryDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var updatedCategory = new Category { Name = "Test" };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/categories/{Guid.NewGuid()}", updatedCategory);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task DeleteCategory_WhenCategoryExists_ReturnsNoContent()
    {
        // Arrange
        var newCategory = new Category
        {
            Name = "Category To Delete"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/categories", newCategory);
        var createdCategory = await createResponse.Content.ReadFromJsonAsync<Category>();

        // Act
        var deleteResponse = await _client.DeleteAsync($"/api/categories/{createdCategory!.Id}");

        // Assert
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // Verify soft delete - category should not appear in list
        var getAllResponse = await _client.GetAsync("/api/categories");
        var categories = await getAllResponse.Content.ReadFromJsonAsync<List<Category>>();
        Assert.That(categories!.Any(c => c.Id == createdCategory.Id), Is.False);
    }

    [Test]
    public async Task DeleteCategory_WhenCategoryDoesNotExist_ReturnsNotFound()
    {
        // Act
        var response = await _client.DeleteAsync($"/api/categories/{Guid.NewGuid()}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetRecipesByCategory_WhenCategoryHasRecipes_ReturnsRecipes()
    {
        // Arrange
        var category = new Category { Name = "Test Category" };
        var createCategoryResponse = await _client.PostAsJsonAsync("/api/categories", category);
        var createdCategory = await createCategoryResponse.Content.ReadFromJsonAsync<Category>();

        var recipe1 = new Recipe
        {
            Title = "Recipe 1",
            CategoryId = createdCategory!.Id
        };
        var recipe2 = new Recipe
        {
            Title = "Recipe 2",
            CategoryId = createdCategory.Id
        };

        await _client.PostAsJsonAsync("/api/recipes", recipe1);
        await _client.PostAsJsonAsync("/api/recipes", recipe2);

        // Act
        var response = await _client.GetAsync($"/api/categories/{createdCategory.Id}/recipes");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var recipes = await response.Content.ReadFromJsonAsync<List<Recipe>>();
        Assert.That(recipes, Is.Not.Null);
        Assert.That(recipes!.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task GetRecipesByCategory_WhenCategoryIsEmpty_ReturnsEmptyList()
    {
        // Arrange
        var category = new Category { Name = "Empty Category" };
        var createCategoryResponse = await _client.PostAsJsonAsync("/api/categories", category);
        var createdCategory = await createCategoryResponse.Content.ReadFromJsonAsync<Category>();

        // Act
        var response = await _client.GetAsync($"/api/categories/{createdCategory!.Id}/recipes");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var recipes = await response.Content.ReadFromJsonAsync<List<Recipe>>();
        Assert.That(recipes, Is.Not.Null);
        Assert.That(recipes, Is.Empty);
    }
}
