namespace ReceptyOks.Shared.Models;

/// <summary>
/// Base class for ingredient data - eliminates duplication across Ingredient, IngredientLocal, and IngredientSyncDto.
/// </summary>
public abstract class IngredientBase
{
    public virtual Guid Id { get; set; }

    /// <summary>
    /// Name of the ingredient, e.g. "Flour", "Sugar".
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Default unit for this ingredient, e.g. "g", "ml", "szt."
    /// </summary>
    public string? Unit { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
