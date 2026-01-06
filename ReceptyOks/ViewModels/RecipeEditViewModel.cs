using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceptyOks.Data;
using System.Collections.ObjectModel;

namespace ReceptyOks.ViewModels;

[QueryProperty(nameof(RecipeIdParam), "id")]
public partial class RecipeEditViewModel : ObservableObject
{
    private readonly LocalDatabase _database;
    private bool _isNewRecipe = true;

    [ObservableProperty]
    private string recipeIdParam = string.Empty;

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private string instructions = string.Empty;

    [ObservableProperty]
    private int preparationTimeMinutes;

    [ObservableProperty]
    private int cookingTimeMinutes;

    [ObservableProperty]
    private int servings = 4;

    [ObservableProperty]
    private byte[]? recipeImage;

    [ObservableProperty]
    private ImageSource? imagePreview;

    [ObservableProperty]
    private CategoryLocal? selectedCategory;

    [ObservableProperty]
    private ObservableCollection<CategoryLocal> categories = [];

    [ObservableProperty]
    private ObservableCollection<EditableIngredient> ingredients = [];

    [ObservableProperty]
    private ObservableCollection<IngredientLocal> availableIngredients = [];

    [ObservableProperty]
    private string pageTitle = "Nowy przepis";

    private Guid _existingId;

    public RecipeEditViewModel(LocalDatabase database)
    {
        _database = database;
    }

    partial void OnRecipeIdParamChanged(string value)
    {
        if (Guid.TryParse(value, out var id))
        {
            _existingId = id;
            _isNewRecipe = false;
            PageTitle = "Edytuj przepis";
            LoadExistingRecipeCommand.Execute(id);
        }
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        var cats = await _database.GetCategoriesAsync();
        Categories = new ObservableCollection<CategoryLocal>(cats);

        var ings = await _database.GetIngredientsAsync();
        AvailableIngredients = new ObservableCollection<IngredientLocal>(ings);
    }

    [RelayCommand]
    private async Task LoadExistingRecipeAsync(Guid id)
    {
        await InitializeAsync();

        var recipe = await _database.GetRecipeAsync(id);
        if (recipe is null) return;

        Title = recipe.Title;
        Description = recipe.Description;
        Instructions = recipe.Instructions;
        PreparationTimeMinutes = recipe.PreparationTimeMinutes;
        CookingTimeMinutes = recipe.CookingTimeMinutes;
        Servings = recipe.Servings;
        RecipeImage = recipe.Image;

        if (recipe.Image is not null)
        {
            ImagePreview = ImageSource.FromStream(() => new MemoryStream(recipe.Image));
        }

        if (recipe.CategoryId.HasValue)
        {
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == recipe.CategoryId.Value);
        }

        var recipeIngredients = await _database.GetRecipeIngredientsAsync(id);
        foreach (var ri in recipeIngredients)
        {
            var ingredient = AvailableIngredients.FirstOrDefault(i => i.Id == ri.IngredientId);
            if (ingredient is not null)
            {
                Ingredients.Add(new EditableIngredient
                {
                    Id = ri.Id,
                    SelectedIngredient = ingredient,
                    Quantity = ri.Quantity,
                    Unit = ri.Unit ?? "",
                    Notes = ri.Notes ?? ""
                });
            }
        }
    }

    [RelayCommand]
    private async Task PickImageAsync()
    {
        try
        {
            var photos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
            {
                Title = "Wybierz zdjêcie przepisu"
            });

            var result = photos?.FirstOrDefault();
            if (result is not null)
            {
                using var stream = await result.OpenReadAsync();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                RecipeImage = memoryStream.ToArray();
                ImagePreview = ImageSource.FromStream(() => new MemoryStream(RecipeImage));
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("B³¹d", $"Nie uda³o siê wybraæ zdjêcia: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private async Task TakePhotoAsync()
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await Shell.Current.DisplayAlertAsync("B³¹d", "Aparat nie jest dostêpny na tym urz¹dzeniu", "OK");
                return;
            }

            var result = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = "Zrób zdjêcie przepisu"
            });

            if (result is not null)
            {
                using var stream = await result.OpenReadAsync();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                RecipeImage = memoryStream.ToArray();
                ImagePreview = ImageSource.FromStream(() => new MemoryStream(RecipeImage));
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("B³¹d", $"Nie uda³o siê zrobiæ zdjêcia: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private void AddIngredient()
    {
        Ingredients.Add(new EditableIngredient
        {
            Id = Guid.NewGuid()
        });
    }

    [RelayCommand]
    private void RemoveIngredient(EditableIngredient ingredient)
    {
        Ingredients.Remove(ingredient);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            await Shell.Current.DisplayAlertAsync("B³¹d", "Podaj tytu³ przepisu", "OK");
            return;
        }

        var recipe = new RecipeLocal
        {
            Id = _isNewRecipe ? Guid.NewGuid() : _existingId,
            Title = Title,
            Description = Description,
            Instructions = Instructions,
            PreparationTimeMinutes = PreparationTimeMinutes,
            CookingTimeMinutes = CookingTimeMinutes,
            Servings = Servings,
            Image = RecipeImage,
            ImageContentType = RecipeImage is not null ? "image/jpeg" : null,
            CategoryId = SelectedCategory?.Id
        };

        await _database.SaveRecipeAsync(recipe);

        // Zapisz sk³adniki
        var recipeIngredients = Ingredients
            .Where(i => i.SelectedIngredient is not null)
            .Select((i, index) => new RecipeIngredientLocal
            {
                Id = i.Id,
                RecipeId = recipe.Id,
                IngredientId = i.SelectedIngredient!.Id,
                Quantity = i.Quantity,
                Unit = i.Unit,
                Notes = i.Notes,
                Order = index
            })
            .ToList();

        await _database.SaveRecipeIngredientsAsync(recipe.Id, recipeIngredients);

        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}

public partial class EditableIngredient : ObservableObject
{
    public Guid Id { get; set; }

    [ObservableProperty]
    private IngredientLocal? selectedIngredient;

    [ObservableProperty]
    private decimal quantity;

    [ObservableProperty]
    private string unit = string.Empty;

    [ObservableProperty]
    private string notes = string.Empty;
}
