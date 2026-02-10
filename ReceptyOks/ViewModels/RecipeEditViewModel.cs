using CommunityToolkit.Maui.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceptyOks.Data;
using ReceptyOks.Services;
using ReceptyOks.Shared;
using ReceptyOks.Shared.OCR;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace ReceptyOks.ViewModels;

[QueryProperty(nameof(RecipeIdParam), "id")]
public partial class RecipeEditViewModel : ObservableObject
{
    private readonly LocalDatabase _database;
    private readonly IOCRService _ocrService;
    private readonly SpeechToTextService _speechToTextService;
    private CancellationTokenSource? _speechCts;
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

    public IReadOnlyList<Jednostki> AvailableUnits { get; } = Enum.GetValues(typeof(Jednostki)).Cast<Jednostki>().ToList();

    [ObservableProperty]
    private string pageTitle = "Nowy przepis";

    [ObservableProperty]
    private bool isListening;

    [ObservableProperty]
    private bool isEditorFullScreen;

    private Guid _existingId;
    private bool _isInitialized = false;

    // Explicit command for removing the image (design-time friendly)
    public ICommand RemoveImageCommand { get; }

    public RecipeEditViewModel(LocalDatabase database, IOCRService ocrService, ISpeechToText speechToText)
    {
        _database = database;
        _ocrService = ocrService;
        _speechToTextService = new SpeechToTextService(speechToText);
        _speechToTextService.RecognitionUpdated += OnRecognitionUpdated;
        _speechToTextService.RecognitionCompleted += OnRecognitionCompleted;

        RemoveImageCommand = new Command(RemoveImageInternal);
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
        else if (!_isInitialized)
        {
            // Jeśli nie ma ID (nowy przepis) i jeszcze nie zainicjalizowano, załaduj kategorie i składniki
            InitializeCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (_isInitialized) return;

        var cats = await _database.GetCategoriesAsync();
        Categories.Clear();
        foreach (var cat in cats)
        {
            Categories.Add(cat);
        }

        var ings = await _database.GetIngredientsAsync();
        AvailableIngredients.Clear();
        foreach (var ing in ings)
        {
            AvailableIngredients.Add(ing);
        }

        _isInitialized = true;
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
                Jednostki selectedUnitLocal = Jednostki.Brak;
                if (!string.IsNullOrWhiteSpace(ri.Unit) && Enum.TryParse<Jednostki>(ri.Unit, out var parsedUnitLocal))
                    selectedUnitLocal = parsedUnitLocal;
                Ingredients.Add(new EditableIngredient
                {
                    Id = ri.Id,
                    SelectedIngredient = ingredient,
                    IngredientName = ingredient.Name,
                    Quantity = ri.Quantity,
                    SelectedUnit = selectedUnitLocal,
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
                Title = "Wybierz zdjęcie przepisu"
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
            await Shell.Current.DisplayAlertAsync("Błąd", $"Nie udało się wybrać zdjęcia: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private async Task TakePhotoAsync()
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await Shell.Current.DisplayAlertAsync("Błąd", "Aparat nie jest dostępny na tym urządzeniu", "OK");
                return;
            }

            var result = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = "Zrób zdjęcie przepisu"
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
            await Shell.Current.DisplayAlertAsync("Błąd", $"Nie udało się zrobić zdjęcia: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private void ToggleEditorFullScreen()
    {
        IsEditorFullScreen = !IsEditorFullScreen;
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
    private void IncrementPreparationTime()
    {
        if (PreparationTimeMinutes < 1000)
            PreparationTimeMinutes++;
    }

    [RelayCommand]
    private void DecrementPreparationTime()
    {
        if (PreparationTimeMinutes > 0)
            PreparationTimeMinutes--;
    }

    [RelayCommand]
    private void IncrementCookingTime()
    {
        if (CookingTimeMinutes < 1000)
            CookingTimeMinutes++;
    }

    [RelayCommand]
    private void DecrementCookingTime()
    {
        if (CookingTimeMinutes > 0)
            CookingTimeMinutes--;
    }

    [RelayCommand]
    private void IncrementServings()
    {
        if (Servings < 100)
            Servings++;
    }

    [RelayCommand]
    private void DecrementServings()
    {
        if (Servings > 1)
            Servings--;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            await Shell.Current.DisplayAlertAsync("Błąd", "Podaj tytuł przepisu", "OK");
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

        // Zapisz składniki
        var recipeIngredients = new List<RecipeIngredientLocal>();
        int order = 0;

        foreach (var ingredient in Ingredients)
        {
            if (string.IsNullOrWhiteSpace(ingredient.IngredientName))
                continue;

            Guid ingredientId;

            // Jeśli składnik został wybrany z listy, użyj jego ID
            if (ingredient.SelectedIngredient is not null)
            {
                ingredientId = ingredient.SelectedIngredient.Id;
            }
            else
            {
                // Jeśli składnik został wpisany, sprawdź czy już istnieje lub utwórz nowy
                var existingIngredient = AvailableIngredients.FirstOrDefault(
                    i => i.Name.Equals(ingredient.IngredientName, StringComparison.OrdinalIgnoreCase));

                if (existingIngredient is not null)
                {
                    ingredientId = existingIngredient.Id;
                }
                else
                {
                    // Utwórz nowy składnik
                    var newIngredient = new IngredientLocal
                    {
                        Id = Guid.NewGuid(),
                        Name = ingredient.IngredientName
                    };
                    await _database.SaveIngredientAsync(newIngredient);
                    AvailableIngredients.Add(newIngredient);
                    ingredientId = newIngredient.Id;
                }
            }

            recipeIngredients.Add(new RecipeIngredientLocal
            {
                Id = ingredient.Id,
                RecipeId = recipe.Id,
                IngredientId = ingredientId,
                Quantity = ingredient.Quantity,
                Unit = ingredient.SelectedUnit != Jednostki.Brak ? ingredient.SelectedUnit.ToString() : null,
                Notes = ingredient.Notes,
                Order = order++
            });
        }

        await _database.SaveRecipeIngredientsAsync(recipe.Id, recipeIngredients);

        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task ScanInstructionsFromCameraAsync()
    {
        try
        {
            var result = await _ocrService.ScanRecipeFromCameraAsync();
            if (result.Success)
            {
                Instructions = result.Text;
            }
            else
            {
                await Shell.Current.DisplayAlertAsync("Błąd", result.ErrorMessage ?? "Nie udało się rozpoznać tekstu", "OK");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Błąd", $"Wystąpił błąd: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private async Task ScanInstructionsFromGalleryAsync()
    {
        try
        {
            var result = await _ocrService.ScanRecipeFromGalleryAsync();
            if (result.Success)
            {
                Instructions = result.Text;
            }
            else
            {
                await Shell.Current.DisplayAlertAsync("Błąd", result.ErrorMessage ?? "Nie udało się rozpoznać tekstu", "OK");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Błąd", $"Wystąpił błąd: {ex.Message}", "OK");
        }
    }
    [RelayCommand]
    private async Task ScanInstructionsFromMicrophoneAsync()
    {
        try
        {
            if (IsListening)
            {
                await StopListeningAsync();
            }
            else
            {
                await StartListeningAsync();
            }
        }
        catch (Exception ex)
        {
            IsListening = false;
            await SnackBarHelper.ShowErrorSnackbarAsync($"Wystąpił błąd: {ex.Message}");
        }
    }

    private async Task StartListeningAsync()
    {
        _speechCts = new CancellationTokenSource();
        IsListening = true;
        await _speechToTextService.StartListeningAsync(_speechCts.Token);
    }

    private async Task StopListeningAsync()
    {
        if (_speechCts is not null)
        {
            await _speechCts.CancelAsync();
            await _speechToTextService.StopListeningAsync(CancellationToken.None);
            _speechCts.Dispose();
            _speechCts = null;
        }
        IsListening = false;
    }

    private void OnRecognitionUpdated(string text)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Instructions = text;
        });
    }

    private void OnRecognitionCompleted(string text)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Instructions = text;
            IsListening = false;
        });
    }

    // Internal method backing the explicit RemoveImageCommand
    private void RemoveImageInternal()
    {
        RecipeImage = null;
        ImagePreview = null;
    }
}
