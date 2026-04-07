using ReceptyOks.Shared.Interfaces;

namespace ReceptyOks.Shared.Models;

/// <summary>
/// Tabela ³¹cz¹ca przepisy ze sk³adnikami (many-to-many) - EF Core entity.
/// </summary>
public class RecipeIngredient : RecipeIngredientBase, IIngredient
{
    public Guid RecipeId { get; set; }

    // Nawigacja
    public Recipe? Recipe { get; set; }
    public Ingredient? Ingredient { get; set; }

    /// <summary>
    /// Name from the linked Ingredient (for IIngredient interface).
    /// </summary>
    public string Name => Ingredient?.Name ?? string.Empty;
}
