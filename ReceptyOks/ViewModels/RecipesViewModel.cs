using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceptyOks.Data;
using ReceptyOks.Services;
using System.Collections.ObjectModel;

namespace ReceptyOks.ViewModels;

public partial class RecipesViewModel : ObservableObject
{
    private readonly LocalDatabase _database;
    private readonly SyncService _syncService;

    [ObservableProperty]
    private ObservableCollection<RecipeLocal> recipes = [];

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private bool isSyncing;

    [ObservableProperty]
    private string searchQuery = string.Empty;

    public RecipesViewModel(LocalDatabase database, SyncService syncService)
    {
        _database = database;
        _syncService = syncService;
    }

    [RelayCommand]
    private async Task LoadRecipesAsync()
    {
        try
        {
            IsRefreshing = true;
            var recipeList = string.IsNullOrWhiteSpace(SearchQuery)
                ? await _database.GetRecipesAsync()
                : await _database.SearchRecipesAsync(SearchQuery);
            
            Recipes = new ObservableCollection<RecipeLocal>(recipeList);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task SyncAsync()
    {
        try
        {
            IsSyncing = true;
            var result = await _syncService.SyncAsync();
            
            if (result.Success)
            {
                await LoadRecipesAsync();
                await Shell.Current.DisplayAlert("Synchronizacja", result.Message, "OK");
            }
            else
            {
                await Shell.Current.DisplayAlert("B³¹d", result.Message, "OK");
            }
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand]
    private async Task GoToAddRecipeAsync()
    {
        await Shell.Current.GoToAsync(nameof(Views.RecipeEditPage));
    }

    [RelayCommand]
    private async Task GoToRecipeDetailAsync(RecipeLocal recipe)
    {
        await Shell.Current.GoToAsync($"{nameof(Views.RecipeDetailPage)}?id={recipe.Id}");
    }

    partial void OnSearchQueryChanged(string value)
    {
        LoadRecipesCommand.Execute(null);
    }
}
