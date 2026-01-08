using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ReceptyOks.Data;
using ReceptyOks.Services;
using System.Collections.ObjectModel;

namespace ReceptyOks.ViewModels;

public partial class RecipesViewModel : ObservableObject
{
    private readonly LocalDatabase _database;
    private readonly SyncService _syncService;
    private readonly ILogger<RecipesViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<RecipeLocal> recipes = [];

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private bool isSyncing;

    [ObservableProperty]
    private string searchQuery = string.Empty;

    public RecipesViewModel(LocalDatabase database, SyncService syncService, ILogger<RecipesViewModel> logger)
    {
        _database = database;
        _syncService = syncService;
        _logger = logger;
        
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
            
            _logger.LogInformation("Successfully loaded {Count} recipes", recipeList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading recipes");
            await Shell.Current.DisplayAlertAsync("Error", "Failed to load recipes", "OK");
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
            _logger.LogInformation("Starting recipe synchronization");
            
            var result = await _syncService.SyncAsync();
            
            if (result.Success)
            {
                _logger.LogInformation("Synchronization successful: {Message}", result.Message);
                await LoadRecipesAsync();
                await Shell.Current.DisplayAlertAsync("Synchronizacja", result.Message, "OK");
            }
            else
            {
                _logger.LogWarning("Synchronization failed: {Message}", result.Message);
                await Shell.Current.DisplayAlertAsync("B³¹d", result.Message, "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during synchronization");
            await Shell.Current.DisplayAlertAsync("Error", "Synchronization failed", "OK");
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

    [RelayCommand]
    private void ClearSearch()
    {
        SearchQuery = string.Empty;
    }

    partial void OnSearchQueryChanged(string value)
    {
        LoadRecipesCommand.Execute(null);
    }
}
