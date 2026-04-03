namespace ReceptyOks.Shared.Models;

/// <summary>
/// Meal plan — assignment of a recipe to a date and time.
/// </summary>
public class MealPlan
{
    public Guid Id { get; set; }

    /// <summary>
    /// Date of the scheduled meal (date only).
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Start hour of the meal (0–23).
    /// </summary>
    public int StartHour { get; set; }

    /// <summary>
    /// Duration of preparation and cooking in minutes.
    /// </summary>
    public int DurationMinutes { get; set; } = 30;

    /// <summary>
    /// ID of the assigned recipe.
    /// </summary>
    public Guid RecipeId { get; set; }

    /// <summary>
    /// Optional note for the meal.
    /// </summary>
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // Nawigacja
    public Recipe? Recipe { get; set; }
}
