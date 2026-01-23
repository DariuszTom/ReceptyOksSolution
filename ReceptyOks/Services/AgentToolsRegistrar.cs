using ReceptyOks.Data;
using ReceptyOks.Shared.AI;
using System.Text.Json;
using ILogger = Serilog.ILogger;

namespace ReceptyOks.Services;

/// <summary>
/// Handles registration and implementation of AI agent tools for database queries.
/// </summary>
public class AgentToolsRegistrar
{
    private readonly LocalDatabase _database;
    private readonly ILogger _logger;

    public AgentToolsRegistrar(LocalDatabase database, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(logger);

        _database = database;
        _logger = logger;
    }

    /// <summary>
    /// Registers all available tools with the AI agent.
    /// </summary>
    public void RegisterTools(AiAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        agent.AddTool<Task<string>>(GetAllRecipesAsync,
              "get_all_recipes", "Retrieves a list of all available recipes with their basic information (title, description, preparation time, cooking time, servings).");

        agent.AddToolAsync<string, string>(SearchRecipesAsync,
               "search_recipes", "Searches for recipes by text query matching title or description. Parameter: searchQuery - the text to search for.");

        agent.AddToolAsync<string, string>(GetRecipeDetailsAsync,
        "get_recipe_details", "Gets detailed information about a specific recipe including ingredients. Parameter: recipeId - the GUID of the recipe.");

        agent.AddTool<Task<string>>(GetAllCategoriesAsync,
          "get_all_categories", "Retrieves all recipe categories with their names and descriptions.");

        agent.AddToolAsync<string, string>(GetRecipesByCategoryAsync,
    "get_recipes_by_category", "Gets all recipes in a specific category. Parameter: categoryId - the GUID of the category.");

        agent.AddTool<Task<string>>(GetAllIngredientsAsync,
               "get_all_ingredients", "Retrieves a list of all available ingredients.");

        _logger.Information("Registered {ToolCount} AI agent tools for database queries", agent.Tools.Count);
    }

    private async Task<string> GetAllRecipesAsync()
    {
        var recipes = await _database.GetRecipesAsync().ConfigureAwait(false);
        var result = recipes.Select(r => new
        {
            r.Id,
            r.Title,
            r.Description,
            r.PreparationTimeMinutes,
            r.CookingTimeMinutes,
            r.Servings,
            r.CategoryId
        });
        return JsonSerializer.Serialize(result);
    }

    private async Task<string> SearchRecipesAsync(string searchQuery)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            return "[]";
        }

        var recipes = await _database.SearchRecipesAsync(searchQuery).ConfigureAwait(false);
        var result = recipes.Select(r => new
        {
            r.Id,
            r.Title,
            r.Description,
            r.PreparationTimeMinutes,
            r.CookingTimeMinutes,
            r.Servings
        });
        return JsonSerializer.Serialize(result);
    }

    private async Task<string> GetRecipeDetailsAsync(string recipeId)
    {
        if (!Guid.TryParse(recipeId, out var id))
        {
            return JsonSerializer.Serialize(new { error = "Invalid recipe ID format" });
        }

        var recipe = await _database.GetRecipeAsync(id).ConfigureAwait(false);
        if (recipe is null)
        {
            return JsonSerializer.Serialize(new { error = "Recipe not found" });
        }

        var recipeIngredients = await _database.GetRecipeIngredientsAsync(id).ConfigureAwait(false);
        var allIngredients = await _database.GetIngredientsAsync().ConfigureAwait(false);

        var ingredientDetails = recipeIngredients
            .Select(ri =>
       {
           var ingredient = allIngredients.FirstOrDefault(i => i.Id == ri.IngredientId);
           return new
           {
               Name = ingredient?.Name ?? "Unknown",
               ri.Quantity,
               Unit = ingredient?.Unit
           };
       })
  .ToList();

        var result = new
        {
            recipe.Id,
            recipe.Title,
            recipe.Description,
            recipe.Instructions,
            recipe.PreparationTimeMinutes,
            recipe.CookingTimeMinutes,
            recipe.Servings,
            recipe.CategoryId,
            Ingredients = ingredientDetails
        };

        return JsonSerializer.Serialize(result);
    }

    private async Task<string> GetAllCategoriesAsync()
    {
        var categories = await _database.GetCategoriesAsync().ConfigureAwait(false);
        var result = categories.Select(c => new
        {
            c.Id,
            c.Name,
            c.Description
        });
        return JsonSerializer.Serialize(result);
    }

    private async Task<string> GetRecipesByCategoryAsync(string categoryId)
    {
        if (!Guid.TryParse(categoryId, out var id))
        {
            return JsonSerializer.Serialize(new { error = "Invalid category ID format" });
        }

        var recipes = await _database.GetRecipesByCategoryAsync(id).ConfigureAwait(false);
        var result = recipes.Select(r => new
        {
            r.Id,
            r.Title,
            r.Description,
            r.PreparationTimeMinutes,
            r.CookingTimeMinutes,
            r.Servings
        });
        return JsonSerializer.Serialize(result);
    }

    private async Task<string> GetAllIngredientsAsync()
    {
        var ingredients = await _database.GetIngredientsAsync().ConfigureAwait(false);
        var result = ingredients.Select(i => new
        {
            i.Id,
            i.Name,
            i.Unit
        });
        return JsonSerializer.Serialize(result);
    }
}
