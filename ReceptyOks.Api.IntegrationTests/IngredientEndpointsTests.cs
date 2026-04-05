using ReceptyOks.Shared.Models;
using System.Net;
using System.Net.Http.Json;

namespace ReceptyOks.Api.IntegrationTests;

[TestFixture]
public class IngredientEndpointsTests
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
    public async Task GetAllIngredients_WhenNoIngredientsExist_ReturnsEmptyList()
    {
        // Act
        var response = await _client.GetAsync("/api/ingredients");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ingredients = await response.Content.ReadFromJsonAsync<List<Ingredient>>();
        Assert.That(ingredients, Is.Not.Null);
        Assert.That(ingredients, Is.Empty);
    }

    [Test]
    public async Task GetAllIngredients_WhenUnauthorized_Returns401()
    {
        // Arrange
        using var unauthenticatedClient = _factory.CreateClient();

        // Act
        var response = await unauthenticatedClient.GetAsync("/api/ingredients");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task CreateIngredient_WithValidData_ReturnsCreated()
    {
        // Arrange
        var newIngredient = new Ingredient
        {
            Name = "Sugar",
            Unit = "g"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/ingredients", newIngredient);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var createdIngredient = await response.Content.ReadFromJsonAsync<Ingredient>();
        Assert.That(createdIngredient, Is.Not.Null);
        Assert.That(createdIngredient!.Name, Is.EqualTo("Sugar"));
        Assert.That(createdIngredient.Unit, Is.EqualTo("g"));
        Assert.That(createdIngredient.Id, Is.Not.EqualTo(Guid.Empty));
    }

    [Test]
    public async Task GetIngredientById_WhenIngredientExists_ReturnsIngredient()
    {
        // Arrange
        var newIngredient = new Ingredient
        {
            Name = "Flour",
            Unit = "kg"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/ingredients", newIngredient);
        var createdIngredient = await createResponse.Content.ReadFromJsonAsync<Ingredient>();

        // Act
        var response = await _client.GetAsync($"/api/ingredients/{createdIngredient!.Id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ingredient = await response.Content.ReadFromJsonAsync<Ingredient>();
        Assert.That(ingredient, Is.Not.Null);
        Assert.That(ingredient!.Name, Is.EqualTo("Flour"));
    }

    [Test]
    public async Task GetIngredientById_WhenIngredientDoesNotExist_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/ingredients/{Guid.NewGuid()}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task UpdateIngredient_WhenIngredientExists_ReturnsUpdatedIngredient()
    {
        // Arrange
        var newIngredient = new Ingredient
        {
            Name = "Original Name",
            Unit = "ml"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/ingredients", newIngredient);
        var createdIngredient = await createResponse.Content.ReadFromJsonAsync<Ingredient>();

        var updatedIngredient = new Ingredient
        {
            Name = "Updated Name",
            Unit = "l"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/ingredients/{createdIngredient!.Id}", updatedIngredient);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<Ingredient>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("Updated Name"));
        Assert.That(result.Unit, Is.EqualTo("l"));
    }

    [Test]
    public async Task UpdateIngredient_WhenIngredientDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var updatedIngredient = new Ingredient { Name = "Test", Unit = "g" };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/ingredients/{Guid.NewGuid()}", updatedIngredient);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task DeleteIngredient_WhenIngredientExists_ReturnsNoContent()
    {
        // Arrange
        var newIngredient = new Ingredient
        {
            Name = "Ingredient To Delete",
            Unit = "szt."
        };
        var createResponse = await _client.PostAsJsonAsync("/api/ingredients", newIngredient);
        var createdIngredient = await createResponse.Content.ReadFromJsonAsync<Ingredient>();

        // Act
        var deleteResponse = await _client.DeleteAsync($"/api/ingredients/{createdIngredient!.Id}");

        // Assert
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // Verify soft delete - ingredient should not appear in list
        var getAllResponse = await _client.GetAsync("/api/ingredients");
        var ingredients = await getAllResponse.Content.ReadFromJsonAsync<List<Ingredient>>();
        Assert.That(ingredients!.Any(i => i.Id == createdIngredient.Id), Is.False);
    }

    [Test]
    public async Task DeleteIngredient_WhenIngredientDoesNotExist_ReturnsNotFound()
    {
        // Act
        var response = await _client.DeleteAsync($"/api/ingredients/{Guid.NewGuid()}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task SearchIngredients_WhenMatchingIngredientsExist_ReturnsFilteredList()
    {
        // Arrange
        var ingredient1 = new Ingredient { Name = "Brown Sugar", Unit = "g" };
        var ingredient2 = new Ingredient { Name = "White Sugar", Unit = "g" };
        var ingredient3 = new Ingredient { Name = "Flour", Unit = "kg" };

        await _client.PostAsJsonAsync("/api/ingredients", ingredient1);
        await _client.PostAsJsonAsync("/api/ingredients", ingredient2);
        await _client.PostAsJsonAsync("/api/ingredients", ingredient3);

        // Act
        var response = await _client.GetAsync("/api/ingredients/search?query=Sugar");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ingredients = await response.Content.ReadFromJsonAsync<List<Ingredient>>();
        Assert.That(ingredients, Is.Not.Null);
        Assert.That(ingredients!.Count, Is.EqualTo(2));
        Assert.That(ingredients.All(i => i.Name.Contains("Sugar")), Is.True);
    }

    [Test]
    public async Task SearchIngredients_WhenNoMatchingIngredients_ReturnsEmptyList()
    {
        // Arrange
        var ingredient = new Ingredient { Name = "Salt", Unit = "g" };
        await _client.PostAsJsonAsync("/api/ingredients", ingredient);

        // Act
        var response = await _client.GetAsync("/api/ingredients/search?query=Pepper");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ingredients = await response.Content.ReadFromJsonAsync<List<Ingredient>>();
        Assert.That(ingredients, Is.Not.Null);
        Assert.That(ingredients, Is.Empty);
    }

    [Test]
    public async Task GetAllIngredients_ReturnsIngredientsOrderedByName()
    {
        // Arrange
        var ingredientC = new Ingredient { Name = "Cinnamon", Unit = "g" };
        var ingredientA = new Ingredient { Name = "Apple", Unit = "szt." };
        var ingredientB = new Ingredient { Name = "Butter", Unit = "g" };

        await _client.PostAsJsonAsync("/api/ingredients", ingredientC);
        await _client.PostAsJsonAsync("/api/ingredients", ingredientA);
        await _client.PostAsJsonAsync("/api/ingredients", ingredientB);

        // Act
        var response = await _client.GetAsync("/api/ingredients");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ingredients = await response.Content.ReadFromJsonAsync<List<Ingredient>>();
        Assert.That(ingredients, Is.Not.Null);
        Assert.That(ingredients!.Count, Is.EqualTo(3));
        Assert.That(ingredients[0].Name, Is.EqualTo("Apple"));
        Assert.That(ingredients[1].Name, Is.EqualTo("Butter"));
        Assert.That(ingredients[2].Name, Is.EqualTo("Cinnamon"));
    }
}
