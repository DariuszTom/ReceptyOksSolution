namespace ReceptyOks.Shared.Models;

/// <summary>
/// Represents the AI-generated shopping list response containing both
/// human-readable text and structured items for programmatic use.
/// </summary>
public class ShoppingListAiResponse
{
    /// <summary>
    /// Human-readable summary or description of the shopping list.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Structured list of shopping items that can be mapped to <see cref="ShoppingListItem"/>.
    /// </summary>
    public List<ShoppingListItemDto> Items { get; set; } = [];
}

/// <summary>
/// DTO representing a single shopping list item from AI response.
/// Maps to <see cref="ShoppingListItem"/> for persistence.
/// </summary>
public class ShoppingListItemDto
{
    /// <summary>
    /// Name of the product/ingredient.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Aggregated quantity (e.g., 500, 2).
    /// </summary>
    public decimal? Quantity { get; set; }

    /// <summary>
    /// Unit of measurement of Jednostki Enum
    /// </summary>
    public Jednostki? Unit { get; set; }

    /// <summary>
    /// Optional note about the ingredient.
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Converts this DTO to a <see cref="ShoppingListItem"/> entity.
    /// </summary>
    public ShoppingListItem ToEntity() => new()
    {
        Id = Guid.NewGuid(),
        Name = Name,
        Quantity = Quantity,
        Unit = Unit?.ToString(),
        Note = Note,
        IsBought = false,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
