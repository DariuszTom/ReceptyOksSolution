using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceptyOks.Data;
using System.Collections.ObjectModel;

namespace ReceptyOks.ViewModels;

public partial class CategoriesViewModel : ObservableObject
{
    private readonly LocalDatabase _database;

    [ObservableProperty]
    private ObservableCollection<CategoryLocal> categories = [];

    [ObservableProperty]
    private bool isRefreshing;

    public CategoriesViewModel(LocalDatabase database)
    {
        _database = database;
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
            "Usuñ kategoriê",
            $"Czy na pewno chcesz usun¹æ kategoriê \"{category.Name}\"?",
            "Usuñ",
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
        await Shell.Current.GoToAsync($"{nameof(Views.RecipesPage)}?categoryId={category.Id}&categoryName={category.Name}");
    }
}
