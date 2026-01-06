namespace ReceptyOks.Shared.Models;

public class Recipe
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public int PreparationTimeMinutes { get; set; }
    public int CookingTimeMinutes { get; set; }
    public int Servings { get; set; }
    
    /// <summary>
    /// Obraz przepisu przechowywany jako BLOB
    /// </summary>
    public byte[]? Image { get; set; }
    
    /// <summary>
    /// Typ MIME obrazu, np. "image/jpeg", "image/png"
    /// </summary>
    public string? ImageContentType { get; set; }
    
    public Guid? CategoryId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// Data ostatniej synchronizacji z serwerem
    /// </summary>
    public DateTime? LastSyncedAt { get; set; }
    
    /// <summary>
    /// Soft delete - oznaczenie usuniêcia bez faktycznego usuwania
    /// </summary>
    public bool IsDeleted { get; set; }
    
    // Nawigacja
    public Category? Category { get; set; }
    public ICollection<RecipeIngredient> Ingredients { get; set; } = [];
}
