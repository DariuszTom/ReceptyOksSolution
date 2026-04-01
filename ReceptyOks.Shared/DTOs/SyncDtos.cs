namespace ReceptyOks.Shared.DTOs;

/// <summary>
/// ¯¹danie synchronizacji - klient wysy³a swoje zmiany
/// </summary>
public class SyncRequest
{
    /// <summary>
    /// Data ostatniej udanej synchronizacji
    /// </summary>
    public DateTime? LastSyncedAt { get; set; }

    /// <summary>
    /// Przepisy zmienione lokalnie od ostatniej synchronizacji
    /// </summary>
    public List<RecipeSyncDto> ChangedRecipes { get; set; } = new List<RecipeSyncDto>();

    /// <summary>
    /// Kategorie zmienione lokalnie
    /// </summary>
    public List<CategorySyncDto> ChangedCategories { get; set; } = new List<CategorySyncDto>();

    /// <summary>
    /// Sk³adniki zmienione lokalnie
    /// </summary>
    public List<IngredientSyncDto> ChangedIngredients { get; set; } = new List<IngredientSyncDto>();

    /// <summary>
    /// Plany posi³ków zmienione lokalnie
    /// </summary>
    public List<MealPlanSyncDto> ChangedMealPlans { get; set; } = new List<MealPlanSyncDto>();
}

/// <summary>
/// OdpowiedŸ synchronizacji - serwer zwraca zmiany do zastosowania
/// </summary>
public class SyncResponse
{
    /// <summary>
    /// Timestamp tej synchronizacji - klient zapisuje jako LastSyncedAt
    /// </summary>
    public DateTime SyncedAt { get; set; }

    /// <summary>
    /// Przepisy do zaktualizowania/dodania na kliencie
    /// </summary>
    public List<RecipeSyncDto> Recipes { get; set; } = new List<RecipeSyncDto>();

    /// <summary>
    /// Kategorie do zaktualizowania/dodania
    /// </summary>
    public List<CategorySyncDto> Categories { get; set; } = new List<CategorySyncDto>();

    /// <summary>
    /// Sk³adniki do zaktualizowania/dodania
    /// </summary>
    public List<IngredientSyncDto> Ingredients { get; set; } = new List<IngredientSyncDto>();

    /// <summary>
    /// Plany posi³ków do zaktualizowania/dodania
    /// </summary>
    public List<MealPlanSyncDto> MealPlans { get; set; } = new List<MealPlanSyncDto>();
}

public class RecipeSyncDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public int PreparationTimeMinutes { get; set; }
    public int CookingTimeMinutes { get; set; }
    public int Servings { get; set; }
    public byte[]? Image { get; set; }
    public string? ImageContentType { get; set; }
    public Guid? CategoryId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public List<RecipeIngredientSyncDto> Ingredients { get; set; } = new List<RecipeIngredientSyncDto>();
}

public class CategorySyncDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

public class IngredientSyncDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

public class RecipeIngredientSyncDto
{
    public Guid Id { get; set; }
    public Guid IngredientId { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public string? Notes { get; set; }
    public int Order { get; set; }
}

public class MealPlanSyncDto
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public int StartHour { get; set; }
    public int DurationMinutes { get; set; }
    public Guid RecipeId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
