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
            Categories = new ObservableCollection<CategoryLocal>(categoryList);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task AddCategoryAsync()
    {
        string name = await Shell.Current.DisplayPromptAsync(
            "Nowa kategoria",
            "Podaj nazwê kategorii:",
            "Dodaj",
            "Anuluj");

        if (!string.IsNullOrWhiteSpace(name))
        {
            var category = new CategoryLocal
            {
                Id = Guid.NewGuid(),
                Name = name
            };

            await _database.SaveCategoryAsync(category);
            await LoadCategoriesAsync();
        }
    }

    [RelayCommand]
    private async Task EditCategoryAsync(CategoryLocal category)
    {
        string name = await Shell.Current.DisplayPromptAsync(
            "Edytuj kategoriê",
            "Podaj now¹ nazwê:",
            "Zapisz",
            "Anuluj",
            initialValue: category.Name);

        if (!string.IsNullOrWhiteSpace(name))
        {
            category.Name = name;
            await _database.SaveCategoryAsync(category);
            await LoadCategoriesAsync();
        }
    }

    [RelayCommand]
    private async Task ViewRecipesInCategoryAsync(CategoryLocal category)
    {
        await Shell.Current.GoToAsync($"{nameof(Views.RecipesPage)}?categoryId={category.Id}&categoryName={category.Name}");
    }
}
