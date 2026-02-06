using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ReceptyOks.Data;
using System.Collections.ObjectModel;

namespace ReceptyOks.ViewModels;

public partial class RandomRecipeViewModel(LocalDatabase database, ILogger<RandomRecipeViewModel> logger) : ObservableObject
{
    private readonly LocalDatabase _database = database;
    private readonly ILogger<RandomRecipeViewModel> _logger = logger;
    private readonly Random _random = new();

    [ObservableProperty]
    private ObservableCollection<CategoryLocal> categories = [];

    [ObservableProperty]
    private ObservableCollection<IngredientLocal> allIngredients = [];

    [ObservableProperty]
    private ObservableCollection<IngredientLocal> selectedIngredients = [];

    [ObservableProperty]
    private CategoryLocal? selectedCategory;

    [ObservableProperty]
    private RecipeLocal? randomRecipe;

    [ObservableProperty]
    private ImageSource? recipeImage;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool hasResult;

    [ObservableProperty]
    private bool filterByCategory = true;

    [ObservableProperty]
    private bool filterByIngredients;

    [ObservableProperty]
    private string ingredientSearchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<IngredientLocal> filteredIngredients = [];
    private Guid OldID;

    partial void OnIngredientSearchQueryChanged(string value)
    {
        FilterIngredients();
    }

    private void FilterIngredients()
    {
        if (string.IsNullOrWhiteSpace(IngredientSearchQuery))
        {
            FilteredIngredients = new ObservableCollection<IngredientLocal>(
                AllIngredients.Where(i => !SelectedIngredients.Any(s => s.Id == i.Id)));
        }
        else
        {
            var query = IngredientSearchQuery;
            FilteredIngredients = new ObservableCollection<IngredientLocal>(
                AllIngredients.Where(i => 
                    i.Name != null &&
                    i.Name.Contains(query, StringComparison.OrdinalIgnoreCase) && 
                    !SelectedIngredients.Any(s => s.Id == i.Id)));
        }
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;

            var categoryList = await _database.GetCategoriesAsync();
            Categories = new ObservableCollection<CategoryLocal>(categoryList);

            var ingredientList = await _database.GetIngredientsAsync();
            AllIngredients = new ObservableCollection<IngredientLocal>(ingredientList);
            FilterIngredients();
            
            if (categoryList.Count == 0)
            {
                _logger.LogWarning("No categories found in database");
            }
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading data for random recipe");
            await Shell.Current.DisplayAlertAsync("Błąd", "Nie udało się załadować danych", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void AddIngredient(IngredientLocal ingredient)
    {
        if (!SelectedIngredients.Any(i => i.Id == ingredient.Id))
        {
            SelectedIngredients.Add(ingredient);
            FilterIngredients();
        }
    }

    [RelayCommand]
    private void RemoveIngredient(IngredientLocal ingredient)
    {
        SelectedIngredients.Remove(ingredient);
        FilterIngredients();
    }

    [RelayCommand]
    private void ClearIngredients()
    {
        SelectedIngredients.Clear();
        FilterIngredients();
    }

    [RelayCommand]
    private async Task RandomizeRecipeAsync()
    {
        try
        {
            IsLoading = true;
            HasResult = false;
            RandomRecipe = null;
            RecipeImage = null;

            List<RecipeLocal> candidates;

            var selectedCatId = (FilterByCategory && SelectedCategory != null) ? SelectedCategory.Id : Guid.Empty;
            var selectedIngIds = FilterByIngredients ? SelectedIngredients.Select(i => i.Id) : null;

            candidates = await _database.GetRecipesByCategoryAndIngriendentsAsync(selectedCatId, selectedIngIds);
            candidates.RemoveAll(r => r.Id == OldID); // Unikaj powtórzeń

            if (candidates.Count == 0)
            {
                OldID = Guid.Empty;
                await Shell.Current.DisplayAlertAsync("Brak przepisów", 
                    "Nie znaleziono przepisów spełniających wybrane kryteria", "OK");
                return;
            }

            var randomIndex = _random.Next(candidates.Count);
            RandomRecipe = candidates[randomIndex];
            HasResult = true;
            OldID = RandomRecipe.Id;

            if (RandomRecipe.Image != null && RandomRecipe.Image.Length > 0)
            {
                RecipeImage = ImageSource.FromStream(() => new MemoryStream(RandomRecipe.Image));
            }

            _logger.LogInformation("Randomly selected recipe: {RecipeTitle}", RandomRecipe.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error randomizing recipe");
            await Shell.Current.DisplayAlertAsync("Błąd", "Nie udało się wylosować przepisu", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task GoToRecipeDetailAsync()
    {
        if (RandomRecipe == null) return;

        await Shell.Current.GoToAsync($"{nameof(Views.RecipeDetailPage)}?id={RandomRecipe.Id}");
    }

    [RelayCommand]
    private void ClearResult()
    {
        RandomRecipe = null;
        RecipeImage = null;
        HasResult = false;
    }
}
