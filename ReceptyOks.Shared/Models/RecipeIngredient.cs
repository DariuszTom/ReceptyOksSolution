namespace ReceptyOks.Shared.Models;

/// <summary>
/// Tabela ³¹cz¹ca przepisy ze sk³adnikami (many-to-many)
/// </summary>
public class RecipeIngredient
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public Guid IngredientId { get; set; }

    /// <summary>
    /// Iloœæ sk³adnika, np. 200
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Jednostka dla tego konkretnego u¿ycia, np. "g", "³y¿ki"
    /// </summary>
    public string? Unit { get; set; }

    /// <summary>
    /// Dodatkowe uwagi, np. "drobno posiekana"
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Kolejnoœæ wyœwietlania sk³adnika w przepisie
    /// </summary>
    public int Order { get; set; }

    // Nawigacja
    public Recipe? Recipe { get; set; }
    public Ingredient? Ingredient { get; set; }
}
