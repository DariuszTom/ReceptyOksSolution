namespace ReceptyOks.Shared.Models;

/// <summary>
/// Base class for recipe-ingredient join data - eliminates duplication across RecipeIngredient, RecipeIngredientLocal, and RecipeIngredientSyncDto.
/// </summary>
public abstract class RecipeIngredientBase
{
    public virtual Guid Id { get; set; }

    /// <summary>
    /// Reference to the ingredient master data.
    /// </summary>
    public virtual Guid IngredientId { get; set; }

    /// <summary>
    /// Quantity of the ingredient used, e.g. 200.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Unit for this specific usage (may differ from ingredient's default unit), e.g. "g", "łyżki".
    /// </summary>
    public string? Unit { get; set; }

    /// <summary>
    /// Additional notes, e.g. "drobno posiekana".
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Display order in the recipe.
    /// </summary>
    public int Order { get; set; }
}
