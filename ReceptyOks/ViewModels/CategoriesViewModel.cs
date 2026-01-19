using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ReceptyOks.Data;
using System.Collections.ObjectModel;

namespace ReceptyOks.ViewModels;

public partial class CategoriesViewModel : ObservableObject
{
    private readonly LocalDatabase _database;
    private readonly ILogger<CategoriesViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<CategoryLocal> categories = [];

    [ObservableProperty]
    private bool isRefreshing;

    public CategoriesViewModel(LocalDatabase database, ILogger<CategoriesViewModel> logger)
    {
        _database = database;
        _logger = logger;
    }

    [RelayCommand]
    private async Task LoadCategoriesAsync()
    {
        try
        {
            IsRefreshing = true;
            var categoryList = await _database.GetCategoriesAsync();
            
            Categories.Clear();
            foreach (var category in categoryList)
            {
                Categories.Add(category);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading categories");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task AddCategoryAsync()
    {
        await Shell.Current.GoToAsync(nameof(Views.CategoryEditPage));
    }

    [RelayCommand]
    private async Task EditCategoryAsync(CategoryLocal category)
    {
        var navigationParameter = new Dictionary<string, object>
        {
            { "category", category }
        };
        await Shell.Current.GoToAsync(nameof(Views.CategoryEditPage), navigationParameter);
    }

    [RelayCommand]
    private async Task DeleteCategoryAsync(CategoryLocal category)
    {
        bool confirm = await Shell.Current.DisplayAlertAsync(
            "Usuń kategorię",
            $"Czy na pewno chcesz usunąć kategorię \"{category.Name}\"?",
            "Usuń",
            "Anuluj");

        if (confirm)
        {
            await _database.DeleteCategoryAsync(category.Id);
            await LoadCategoriesAsync();
        }
    }

    [RelayCommand]
    private async Task ViewRecipesInCategoryAsync(CategoryLocal category)
    {
        await EditCategoryAsync(category);
    }
}
