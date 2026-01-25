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

        agent.AddToolAsync<string, string>(AddRecipeToDBAsync,
          "add_recipe", "Adds a new recipe to the database. Parameter: recipeJson - JSON string containing recipe details (Title, Description, Instructions, PreparationTimeMinutes, CookingTimeMinutes, Servings, CategoryId, Ingredients array with Name, Quantity, Unit).");

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
    private async Task<string> AddRecipeToDBAsync(string recipeJson)
    {
        if (string.IsNullOrWhiteSpace(recipeJson))
        {
            return JsonSerializer.Serialize(new { success = false, error = "Recipe JSON cannot be empty" });
        }

        try
        {
            var recipeData = JsonSerializer.Deserialize<RecipeAddRequest>(recipeJson);
            if (recipeData is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Failed to parse recipe JSON" });
            }

            if (string.IsNullOrWhiteSpace(recipeData.Title))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Recipe title is required" });
            }

            var recipeId = Guid.NewGuid();
            var recipe = new RecipeLocal
            {
                Id = recipeId,
                Title = recipeData.Title,
                Description = recipeData.Description ?? string.Empty,
                Instructions = recipeData.Instructions ?? string.Empty,
                PreparationTimeMinutes = recipeData.PreparationTimeMinutes,
                CookingTimeMinutes = recipeData.CookingTimeMinutes,
                Servings = recipeData.Servings > 0 ? recipeData.Servings : 1,
                CategoryId = recipeData.CategoryId != Guid.Empty ? recipeData.CategoryId : null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _database.SaveRecipeAsync(recipe).ConfigureAwait(false);

            if (recipeData.Ingredients is not null && recipeData.Ingredients.Any())
            {
                var allIngredients = await _database.GetIngredientsAsync().ConfigureAwait(false);
                var recipeIngredients = new List<RecipeIngredientLocal>();
                int order = 0;

                foreach (var ingredientData in recipeData.Ingredients)
                {
                    if (string.IsNullOrWhiteSpace(ingredientData.Name))
                        continue;

                    var existingIngredient = allIngredients.FirstOrDefault(
             i => i.Name.Equals(ingredientData.Name, StringComparison.OrdinalIgnoreCase));

                    Guid ingredientId;
                    if (existingIngredient is not null)
                    {
                        ingredientId = existingIngredient.Id;
                    }
                    else
                    {
                        var newIngredient = new IngredientLocal
                        {
                            Id = Guid.NewGuid(),
                            Name = ingredientData.Name,
                            Unit = ingredientData.Unit,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        await _database.SaveIngredientAsync(newIngredient).ConfigureAwait(false);
                        allIngredients.Add(newIngredient);
                        ingredientId = newIngredient.Id;
                    }

                    recipeIngredients.Add(new RecipeIngredientLocal
                    {
                        Id = Guid.NewGuid(),
                        RecipeId = recipeId,
                        IngredientId = ingredientId,
                        Quantity = ingredientData.Quantity,
                        Unit = ingredientData.Unit,
                        Notes = ingredientData.Notes,
                        Order = order++
                    });
                }

                await _database.SaveRecipeIngredientsAsync(recipeId, recipeIngredients).ConfigureAwait(false);
            }

            _logger.Information("AI agent successfully added recipe: {RecipeTitle} (ID: {RecipeId})", recipeData.Title, recipeId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                recipeId = recipeId,
                message = $"Recipe '{recipeData.Title}' has been successfully added to the database"
            });
        }
        catch (JsonException ex)
        {
            _logger.Error(ex, "Failed to parse recipe JSON for AI agent");
            return JsonSerializer.Serialize(new { success = false, error = $"Invalid JSON format: {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to add recipe via AI agent");
            return JsonSerializer.Serialize(new { success = false, error = $"Failed to add recipe: {ex.Message}" });
        }
    }

    private class RecipeAddRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Instructions { get; set; }
        public int PreparationTimeMinutes { get; set; }
        public int CookingTimeMinutes { get; set; }
        public int Servings { get; set; }
        public Guid CategoryId { get; set; }
        public List<IngredientAddRequest>? Ingredients { get; set; }
    }

    private class IngredientAddRequest
    {
        public string Name { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string? Unit { get; set; }
        public string? Notes { get; set; }
    }

}

