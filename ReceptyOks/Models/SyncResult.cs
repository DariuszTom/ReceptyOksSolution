namespace ReceptyOks.Services;

public class SyncResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int RecipesSynced { get; set; }
    public int CategoriesSynced { get; set; }
    public int IngredientsSynced { get; set; }
}
