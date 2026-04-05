
namespace ReceptyOks.Shared.Models;

public class Ingredient
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Unit { get; set; } // np. "g", "ml", "szt."
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // Nawigacja
    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = [];
}
