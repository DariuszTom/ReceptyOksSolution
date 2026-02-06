namespace ReceptyOks.Shared.Models;

/// <summary>
/// Reprezentuje element listy zakupów przechowywanej centralnie na backendzie.
/// Pozwala na współdzielenie listy między użytkownikami bez rozbieżności.
/// </summary>
public class ShoppingListItem
{
    public Guid Id { get; set; }

    /// <summary>
    /// Nazwa produktu do kupienia.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Ilość produktu (np. "2", "500").
    /// </summary>
    public decimal? Quantity { get; set; }

    /// <summary>
    /// Jednostka miary (np. "szt.", "kg", "ml").
    /// </summary>
    public string? Unit { get; set; }

    /// <summary>
    /// Czy produkt został już kupiony.
    /// </summary>
    public bool IsBought { get; set; }

    /// <summary>
    /// Kto oznaczył jako kupione (opcjonalnie).
    /// </summary>
    public string? BoughtBy { get; set; }

    /// <summary>
    /// Kiedy oznaczono jako kupione.
    /// </summary>
    public DateTime? BoughtAt { get; set; }

    /// <summary>
    /// Opcjonalna notatka/komentarz.
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Opcjonalne powiązanie ze składnikiem z bazy.
    /// </summary>
    public Guid? IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }

    /// <summary>
    /// Opcjonalne powiązanie z przepisem (skąd pochodzi składnik).
    /// </summary>
    public Guid? RecipeId { get; set; }
    public Recipe? Recipe { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
