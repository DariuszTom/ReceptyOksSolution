using ReceptyOks.Interfaces;
using ReceptyOks.Models;

namespace ReceptyOks.ViewModels;

public partial class IngriednientsCalculationViewModel(ILocalDatabase database) : ObservableObject
{
    private readonly ILocalDatabase _database = database;

    [ObservableProperty]
    private ObservableCollection<RecipeLocal> recipes = [];

    [ObservableProperty]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    private RecipeLocal? selectedRecipe;

    [ObservableProperty]
    private ObservableCollection<RecipeIngredientDisplay> ingredients = [];

    [ObservableProperty]
    private bool isLoading;

    [RelayCommand]
    private async Task LoadRecipesAsync()
    {
        try
        {
            IsLoading = true;
            var recipeList = string.IsNullOrWhiteSpace(SearchQuery)
                ? await _database.GetRecipesAsync()
                : await _database.SearchRecipesAsync(SearchQuery);

            Recipes = new ObservableCollection<RecipeLocal>(recipeList);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadIngredientsAsync(Guid recipeId)
    {
        try
        {
            IsLoading = true;
            var recipeIngredients = await _database.GetRecipeIngredientsAsync(recipeId);
            var allIngredients = await _database.GetIngredientsAsync();

            var displayItems = recipeIngredients
                .OrderBy(ri => ri.Order)
                .Select(ri =>
                {
                    var ingredient = allIngredients.FirstOrDefault(i => i.Id == ri.IngredientId);
                    return new RecipeIngredientDisplay
                    {
                        Name = ingredient?.Name ?? "Nieznany",
                        Quantity = ri.Quantity,
                        Unit = ri.Unit ?? ingredient?.Unit ?? "",
                        Notes = ri.Notes
                    };
                })
                .ToList();

            Ingredients = new ObservableCollection<RecipeIngredientDisplay>(displayItems);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ClearSelection()
    {
        SelectedRecipe = null;
        SearchQuery = string.Empty;
        Ingredients.Clear();
    }
}
