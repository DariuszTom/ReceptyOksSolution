using AsyncAwaitBestPractices;
using ReceptyOks.Shared.Misc;
using ReceptyOks.Services;
using Microsoft.Maui.ApplicationModel;

namespace ReceptyOks.ViewModels;

public partial class IngredientsCalculationViewModel(ILocalDatabase database) : ObservableObject
{
    private readonly ILocalDatabase _database = database;

    [ObservableProperty]
    private ObservableCollection<RecipeSummary> recipes = [];

    public FormShape[] FormTypes { get; } = Enum.GetValues<FormShape>();

    [ObservableProperty]
    private RecipeSummary? selectedRecipe;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOriginalFormCircular))]
    private FormShape originalFormShape = FormShape.Circular;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNewFormCircular))]
    private FormShape newFormShape = FormShape.Circular;

    public bool IsOriginalFormCircular => OriginalFormShape == FormShape.Circular;
    public bool IsNewFormCircular => NewFormShape == FormShape.Circular;

    partial void OnOriginalFormShapeChanged(FormShape value) => OriginalForm.Shape = value;
    partial void OnNewFormShapeChanged(FormShape value) => NewForm.Shape = value;
    partial void OnSelectedRecipeChanged(RecipeSummary? value) => RecipeSelectedCommand.ExecuteAsync(value).SafeFireAndForget();

    [RelayCommand]
    private async Task LoadRecipesAsync()
    {
        try
        {
            IsLoading = true;
            var recipeList = await _database.GetRecipeSummariesAsync();
            Recipes = new ObservableCollection<RecipeSummary>(recipeList);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RecipeSelectedAsync(RecipeSummary? recipe)
    {
        if (recipe is null)
            return;

        try
        {
            IsLoading = true;
            var recipeIngredients = await _database.GetRecipeIngredientsAsync(recipe.Id);
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
    private  void CalculateScaledIngredients()
    {
        if (Ingredients.Count == 0)
            return;
        try
        {
            ScalingMultiplier = FormCalculator.CalculateMultiplier(OriginalForm, NewForm);
            var scaled = FormCalculator.ScaleIngredients(Ingredients, OriginalForm, NewForm);
            ScaledIngredients = new ObservableCollection<ScaledIngredient>(scaled);
        }
        catch (DivideByZeroException)
        {
            // Wymiary formy nie mogą być zerowe - pokaż informację użytkownikowi
            MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await SnackBarHelper.ShowErrorSnackbarAsync("Błąd: wymiar formy nie może być zerowy.");
            }).SafeFireAndForget();
        }

    }

    [RelayCommand]
    private void ClearSelection()
    {
        SelectedRecipe = null;
        Ingredients.Clear();
        ScaledIngredients.Clear();
        ScalingMultiplier = 1;
    }
}
