namespace ReceptyOks.Services;

public class SyncResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int RecipesSynced { get; set; }
    public int CategoriesSynced { get; set; }
    public int IngredientsSynced { get; set; }
    public int MealPlansSynced { get; set; }
}

/// <summary>
/// Per-type failure counts returned by ApplyServerChangesAsync.
/// </summary>
public record ApplyResult(
    int FailedCategories,
    int FailedIngredients,
    int FailedRecipes,
    int FailedMealPlans)
{
    public int TotalFailed => FailedCategories + FailedIngredients + FailedRecipes + FailedMealPlans;
}
