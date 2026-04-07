
namespace ReceptyOks.Shared.Models;

/// <summary>
/// Ingredient master data (EF Core entity).
/// </summary>
public class Ingredient : IngredientBase
{
    // Nawigacja
    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = [];
}
