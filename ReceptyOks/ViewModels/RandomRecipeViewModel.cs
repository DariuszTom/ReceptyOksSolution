using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ReceptyOks.Data;
using ReceptyOks.Shared.Models;
using System.Collections.ObjectModel;

namespace ReceptyOks.ViewModels;

public partial class RandomRecipeViewModel : ObservableObject
{
    private readonly LocalDatabase _database;
    private readonly ILogger<RandomRecipeViewModel> _logger;
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

    public RandomRecipeViewModel(LocalDatabase database, ILogger<RandomRecipeViewModel> logger)
    {
        _database = database;
        _logger = logger;
    }

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
            var query = IngredientSearchQuery.ToLower();
            FilteredIngredients = new ObservableCollection<IngredientLocal>(
                AllIngredients.Where(i => 
                    i.Name.ToLower().Contains(query) && 
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
            await Shell.Current.DisplayAlertAsync("B³¹d", "Nie uda³o siê za³adowaæ danych", "OK");
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

            if (FilterByCategory && SelectedCategory != null)
            {
                candidates = await _database.GetRecipesByCategoryAsync(SelectedCategory.Id);
                _logger.LogDebug("Found {Count} recipes in category {Category}", candidates.Count, SelectedCategory.Name);
            }
            else
            {
                candidates = await _database.GetRecipesAsync();
                _logger.LogDebug("Found {Count} total recipes", candidates.Count);
            }
            candidates.RemoveAll(r => r.Id == OldID); // Unikaj powtórzeñ
            if (FilterByIngredients && SelectedIngredients.Count > 0)
            {
                var selectedIngredientIds = SelectedIngredients.Select(i => i.Id).ToHashSet();
                var filteredCandidates = new List<RecipeLocal>();

                foreach (var recipe in candidates)
                {
                    var recipeIngredients = await _database.GetRecipeIngredientsAsync(recipe.Id);
                    var recipeIngredientIds = recipeIngredients.Select(ri => ri.IngredientId).ToHashSet();

                    // SprawdŸ czy przepis zawiera wszystkie wybrane sk³adniki
                    if (selectedIngredientIds.All(id => recipeIngredientIds.Contains(id)))
                    {
                        filteredCandidates.Add(recipe);
                    }
                }

                candidates = filteredCandidates;
                _logger.LogDebug("After ingredient filter: {Count} recipes", candidates.Count);
            }

            if (candidates.Count == 0)
            {
                await Shell.Current.DisplayAlertAsync("Brak przepisów", 
                    "Nie znaleziono przepisów spe³niaj¹cych wybrane kryteria", "OK");
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
            await Shell.Current.DisplayAlertAsync("B³¹d", "Nie uda³o siê wylosowaæ przepisu", "OK");
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
