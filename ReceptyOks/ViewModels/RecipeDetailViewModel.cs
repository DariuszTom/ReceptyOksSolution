using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceptyOks.Data;
using System.Collections.ObjectModel;

namespace ReceptyOks.ViewModels;

[QueryProperty(nameof(RecipeId), "id")]
public partial class RecipeDetailViewModel : ObservableObject
{
    private readonly LocalDatabase _database;

    [ObservableProperty]
    private string recipeId = string.Empty;

    [ObservableProperty]
    private RecipeLocal? recipe;

    [ObservableProperty]
    private CategoryLocal? category;

    [ObservableProperty]
    private ObservableCollection<RecipeIngredientDisplay> ingredients = [];

    [ObservableProperty]
    private ImageSource? recipeImage;

    public RecipeDetailViewModel(LocalDatabase database)
    {
        _database = database;
    }

    partial void OnRecipeIdChanged(string value)
    {
        if (Guid.TryParse(value, out var id))
        {
            LoadRecipeCommand.Execute(id);
        }
    }

    [RelayCommand]
    private async Task LoadRecipeAsync(Guid id)
    {
        Recipe = await _database.GetRecipeAsync(id);
        
        if (Recipe is null) return;

        // Załaduj kategorię
        if (Recipe.CategoryId.HasValue)
        {
            Category = await _database.GetCategoryAsync(Recipe.CategoryId.Value);
        }

        // Załaduj składniki z nazwami
        var recipeIngredients = await _database.GetRecipeIngredientsAsync(id);
        var allIngredients = await _database.GetIngredientsAsync();
        
        var displayIngredients = recipeIngredients.Select(ri =>
        {
            var ingredient = allIngredients.FirstOrDefault(i => i.Id == ri.IngredientId);
            return new RecipeIngredientDisplay
            {
                Name = ingredient?.Name ?? "Nieznany",
                Quantity = ri.Quantity,
                Unit = ri.Unit ?? ingredient?.Unit ?? "",
                Notes = ri.Notes
            };
        }).ToList();

        Ingredients = new ObservableCollection<RecipeIngredientDisplay>(displayIngredients);

        // Załaduj obraz
        if (Recipe.Image is not null && Recipe.Image.Length > 0)
        {
            RecipeImage = ImageSource.FromStream(() => new MemoryStream(Recipe.Image));
        }
    }

    [RelayCommand]
    private async Task EditRecipeAsync()
    {
        if (Recipe is not null)
        {
            await Shell.Current.GoToAsync($"{nameof(Views.RecipeEditPage)}?id={Recipe.Id}");
        }
    }

    [RelayCommand]
    private async Task DeleteRecipeAsync()
    {
        if (Recipe is null) return;

        bool confirm = await Shell.Current.DisplayAlertAsync(
            "Usuwanie przepisu",
            $"Czy na pewno chcesz usunąć '{Recipe.Title}'?",
            "Tak", "Nie");

        if (confirm)
        {
            await _database.DeleteRecipeAsync(Recipe.Id);
            await Shell.Current.GoToAsync("..");
        }
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}

public class RecipeIngredientDisplay
{
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Notes { get; set; }
    
    public string DisplayText => $"{Quantity} {Unit} {Name}" + (string.IsNullOrEmpty(Notes) ? "" : $" ({Notes})");
}
