namespace ReceptyOks.Shared.Models;

/// <summary>
/// Plan posiłku — przypisanie przepisu do daty i godziny.
/// </summary>
public class MealPlan
{
    public Guid Id { get; set; }

    /// <summary>
    /// Data zaplanowanego posiłku (bez czasu).
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Godzina rozpoczęcia posiłku (0–23).
    /// </summary>
    public int StartHour { get; set; }

    /// <summary>
    /// Czas trwania przygotowania i gotowania w minutach.
    /// </summary>
    public int DurationMinutes { get; set; } = 30;

    /// <summary>
    /// ID przypisanego przepisu.
    /// </summary>
    public Guid RecipeId { get; set; }

    /// <summary>
    /// Opcjonalna notatka do posiłku.
    /// </summary>
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // Nawigacja
    public Recipe? Recipe { get; set; }
}
