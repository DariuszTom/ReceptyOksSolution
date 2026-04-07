using ReceptyOks.Interfaces;
using ReceptyOks.Models;
using ReceptyOks.Shared.Misc;

namespace ReceptyOks.ViewModels;

public partial class IngredientsCalculationViewModel(ILocalDatabase database) : ObservableObject
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
    private ObservableCollection<ScaledIngredient> scaledIngredients = [];

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private BakingForm originalForm = BakingForm.Circular(24);

    [ObservableProperty]
    private BakingForm newForm = BakingForm.Circular(26);

    [ObservableProperty]
    private decimal scalingMultiplier = 1;

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
            ScaledIngredients.Clear();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void CalculateScaledIngredients()
    {
        if (Ingredients.Count == 0)
            return;

        ScalingMultiplier = FormCalculator.CalculateMultiplier(OriginalForm, NewForm);
        var scaled = FormCalculator.ScaleIngredients(Ingredients, OriginalForm, NewForm);
        ScaledIngredients = new ObservableCollection<ScaledIngredient>(scaled);
    }

    [RelayCommand]
    private void ClearSelection()
    {
        SelectedRecipe = null;
        SearchQuery = string.Empty;
        Ingredients.Clear();
        ScaledIngredients.Clear();
        ScalingMultiplier = 1;
    }
}
