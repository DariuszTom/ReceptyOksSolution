namespace ReceptyOks.Shared.Models;

/// <summary>
/// Represents an item in the centralized shopping list stored on the backend.
/// Allows sharing the list between users without discrepancies.
/// </summary>
public class ShoppingListItem
{
    public Guid Id { get; set; }

    /// <summary>
    /// Product name to purchase.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Quantity of the product (e.g., "2", "500").
    /// </summary>
    public decimal? Quantity { get; set; }

    /// <summary>
    /// Unit of measure (e.g., "pcs", "kg", "ml").
    /// </summary>
    public string? Unit { get; set; }

    /// <summary>
    /// Whether the product has already been bought.
    /// </summary>
    public bool IsBought { get; set; }

    /// <summary>
    /// Who marked it as bought (optional).
    /// </summary>
    public string? BoughtBy { get; set; }

    /// <summary>
    /// When it was marked as bought.
    /// </summary>
    public DateTime? BoughtAt { get; set; }

    /// <summary>
    /// Optional note/comment.
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Optional link to an ingredient from the database.
    /// </summary>
    public Guid? IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }

    /// <summary>
    /// Optional link to the recipe (where the ingredient originated).
    /// </summary>
    public Guid? RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
