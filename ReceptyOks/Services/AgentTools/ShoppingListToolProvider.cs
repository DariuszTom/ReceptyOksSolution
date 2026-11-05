using ReceptyOks.Interfaces;
using ReceptyOks.Shared.AI;
using System.Text.Json;

namespace ReceptyOks.Services.AgentTools;

/// <summary>Tools used by the shopping list agent (aggregated ingredients of selected recipes).</summary>
public sealed class ShoppingListToolProvider(LocalDatabase database) : IAgentToolProvider
{
    private readonly LocalDatabase _database = database ?? throw new ArgumentNullException(nameof(database));

    public AgentToolGroup Group => AgentToolGroup.ShoppingList;

    public void Register(AiAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        agent.AddToolAsync<List<Guid>, string>(GetAllIngredientsForRecipesAsync,
            "get_all_ingredients_for_recipes", "Parameter: List of recipeId strings (GUIDs).");
    }

    private async Task<string> GetAllIngredientsForRecipesAsync(List<Guid> list)
    {
        if (list is null || list.Count == 0)
        {
            return "[]";
        }

        var validIds = list.Where(id => id != Guid.Empty).Distinct().ToList();
        if (validIds.Count == 0)
        {
            return "[]";
        }

        var allRecipeIngredients = new List<RecipeIngredientLocal>();
        foreach (var recipeId in validIds)
        {
            var recipeIngredients = await _database.GetRecipeIngredientsAsync(recipeId).ConfigureAwait(false);
            allRecipeIngredients.AddRange(recipeIngredients);
        }

        var neededIds = allRecipeIngredients.Select(ri => ri.IngredientId).ToHashSet();
        var ingredients = await _database.GetIngredientsAsync().ConfigureAwait(false);
        var ingredientLookup = ingredients
            .Where(i => neededIds.Contains(i.Id))
            .ToDictionary(i => i.Id);

        var aggregated = new Dictionary<Guid, (string Name, decimal Quantity, string? Unit)>();
        foreach (var ri in allRecipeIngredients)
        {
            var name = ingredientLookup.TryGetValue(ri.IngredientId, out var ingredient)
                ? ingredient.Name
                : "Unknown";
            var unit = ingredient?.Unit;

            if (aggregated.TryGetValue(ri.IngredientId, out var existing))
            {
                aggregated[ri.IngredientId] = (existing.Name, existing.Quantity + ri.Quantity, existing.Unit);
            }
            else
            {
                aggregated[ri.IngredientId] = (name, ri.Quantity, unit);
            }
        }

        var result = aggregated.Values.Select(v => new
        {
            v.Name,
            v.Quantity,
            v.Unit
        });

        return JsonSerializer.Serialize(result);
    }
}
