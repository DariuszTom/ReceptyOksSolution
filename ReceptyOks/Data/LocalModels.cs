using SQLite;

namespace ReceptyOks.Data;

/// <summary>
/// Lokalne tabele SQLite dla MAUI (sqlite-net-pcl u¿ywa atrybutów, nie EF)
/// </summary>
[Table("Recipes")]
public class RecipeLocal
{
    [PrimaryKey]
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
    public DateTime? LastSyncedAt { get; set; }
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Czy rekord wymaga synchronizacji z serwerem
    /// </summary>
    public bool IsDirty { get; set; }
}

[Table("Categories")]
public class CategoryLocal
{
    [PrimaryKey]
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsDirty { get; set; }
}

[Table("Ingredients")]
public class IngredientLocal
{
    [PrimaryKey]
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsDirty { get; set; }
}

[Table("RecipeIngredients")]
public class RecipeIngredientLocal
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid RecipeId { get; set; }

    [Indexed]
    public Guid IngredientId { get; set; }

    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public string? Notes { get; set; }
    public int Order { get; set; }
}

[Table("SyncInfo")]
public class SyncInfo
{
    [PrimaryKey]
    public int Id { get; set; } = 1;
    public DateTime? LastSyncedAt { get; set; }
}

[Table("Logs")]
public class LogEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public DateTime Timestamp { get; set; }

    public string Level { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? Exception { get; set; }

    public string? Properties { get; set; }
}

/// <summary>
/// Model przechowuj¹cy przypisanie przepisu do konkretnego dnia i typu posi³ku.
/// </summary>
[Table("MealPlans")]
public class MealPlanLocal
{
    [PrimaryKey]
    public Guid Id { get; set; }

    /// <summary>
    /// Data zaplanowanego posi³ku (bez czasu).
    /// </summary>
    [Indexed]
    public DateTime Date { get; set; }

    /// <summary>
    /// Typ posi³ku: 0=Œniadanie, 1=Obiad, 2=Kolacja, 3=Przek¹ska.
    /// </summary>
    public int MealType { get; set; }

    /// <summary>
    /// ID przypisanego przepisu.
    /// </summary>
    [Indexed]
    public Guid RecipeId { get; set; }

    /// <summary>
    /// Opcjonalna notatka do posi³ku.
    /// </summary>
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsDirty { get; set; }
}

/// <summary>
/// Enum dla typów posi³ków.
/// </summary>
public enum MealType
{
    Breakfast = 0,  // Œniadanie
    Lunch = 1,  // Obiad
    Dinner = 2,   // Kolacja
    Snack = 3       // Przek¹ska
}
