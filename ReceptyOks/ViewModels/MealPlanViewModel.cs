using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ReceptyOks.Data;
using System.Collections.ObjectModel;

namespace ReceptyOks.ViewModels;

/// <summary>
/// ViewModel dla strony planowania menu na tydzień/dzień.
/// </summary>
public partial class MealPlanViewModel : ObservableObject
{
    private readonly LocalDatabase _database;
    private readonly ILogger<MealPlanViewModel> _logger;

    [ObservableProperty]
    private DateTime currentWeekStart;

    [ObservableProperty]
    private string weekRangeText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<DayPlanItem> weekDays = [];

    [ObservableProperty]
    private ObservableCollection<RecipeLocal> availableRecipes = [];

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isRecipePickerVisible;

    [ObservableProperty]
    private bool isCategoryStepVisible;

    [ObservableProperty]
    private bool isRecipeStepVisible;

    [ObservableProperty]
    private ObservableCollection<CategoryLocal> availableCategories = [];

    [ObservableProperty]
    private CategoryLocal? selectedCategory;

    [ObservableProperty]
    private string recipeSearchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<RecipeLocal> filteredRecipes = [];

    private DayPlanItem? _selectedDayForAdding;

    public MealPlanViewModel(LocalDatabase database, ILogger<MealPlanViewModel> logger)
    {
        _database = database;
        _logger = logger;
        CurrentWeekStart = GetStartOfWeek(DateTime.Today);
    }

    partial void OnCurrentWeekStartChanged(DateTime value)
    {
        UpdateWeekRangeText();
    }

    partial void OnRecipeSearchQueryChanged(string value)
    {
        FilterRecipes();
    }

    private void UpdateWeekRangeText()
    {
        var endOfWeek = CurrentWeekStart.AddDays(6);
        WeekRangeText = $"{CurrentWeekStart:dd MMM} - {endOfWeek:dd MMM yyyy}";
    }

    private void FilterRecipes()
    {
        FilterRecipesByCategory();
    }

    private void FilterRecipesByCategory()
    {
        var source = SelectedCategory is not null
            ? AvailableRecipes.Where(r => r.CategoryId == SelectedCategory.Id)
            : AvailableRecipes;

        if (!string.IsNullOrWhiteSpace(RecipeSearchQuery))
        {
            source = source.Where(r =>
                r.Title.Contains(RecipeSearchQuery, StringComparison.OrdinalIgnoreCase) ||
                r.Description.Contains(RecipeSearchQuery, StringComparison.OrdinalIgnoreCase));
        }

        FilteredRecipes = new ObservableCollection<RecipeLocal>(source);
    }

    private static DateTime GetStartOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }

    /// <summary>
    /// Ładuje dane dla bieżącego tygodnia.
    /// </summary>
    [RelayCommand]
    private async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            UpdateWeekRangeText();

            var categories = await _database.GetCategoriesAsync();
            AvailableCategories = new ObservableCollection<CategoryLocal>(categories);

            var recipes = await _database.GetRecipesAsync();
            AvailableRecipes = new ObservableCollection<RecipeLocal>(recipes);
            FilterRecipes();

            var endOfWeek = CurrentWeekStart.AddDays(6);
            var mealPlansWithRecipes = await _database.GetMealPlansWithRecipesAsync(CurrentWeekStart, endOfWeek);

            var days = new ObservableCollection<DayPlanItem>();
            for (var i = 0; i < 7; i++)
            {
                var date = CurrentWeekStart.AddDays(i);
                var dayItem = new DayPlanItem
                {
                    Date = date,
                    DayName = GetPolishDayName(date.DayOfWeek),
                    DateText = date.ToString("dd.MM"),
                    IsToday = date.Date == DateTime.Today,
                    IsPastDay = date.Date < DateTime.Today
                };

                var dayMeals = mealPlansWithRecipes.Where(mp => mp.MealPlan.Date.Date == date.Date).ToList();

                foreach (var item in dayMeals)
                {
                    dayItem.Meals.Add(new MealItem
                    {
                        Id = item.MealPlan.Id,
                        Recipe = item.Recipe,
                        Notes = item.MealPlan.Notes
                    });
                }

                days.Add(dayItem);
            }

            WeekDays = days;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading meal plan data");
            await Shell.Current.DisplayAlertAsync("Błąd", "Nie udało się załadować planu posiłków", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Przechodzi do poprzedniego tygodnia.
    /// </summary>
    [RelayCommand]
    private async Task PreviousWeekAsync()
    {
        CurrentWeekStart = CurrentWeekStart.AddDays(-7);
        await LoadDataAsync();
    }

    /// <summary>
    /// Przechodzi do następnego tygodnia.
    /// </summary>
    [RelayCommand]
    private async Task NextWeekAsync()
    {
        CurrentWeekStart = CurrentWeekStart.AddDays(7);
        await LoadDataAsync();
    }

    /// <summary>
    /// Przechodzi do bieżącego tygodnia.
    /// </summary>
    [RelayCommand]
    private async Task GoToTodayAsync()
    {
        CurrentWeekStart = GetStartOfWeek(DateTime.Today);
        await LoadDataAsync();
    }

    /// <summary>
    /// Otwiera picker — zaczyna od wyboru kategorii.
    /// </summary>
    [RelayCommand]
    private void OpenRecipePicker(DayPlanItem dayPlan)
    {
        if (dayPlan.IsPastDay) return;

        _selectedDayForAdding = dayPlan;
        SelectedCategory = null;
        RecipeSearchQuery = string.Empty;
        IsCategoryStepVisible = true;
        IsRecipeStepVisible = false;
        IsRecipePickerVisible = true;
    }

    /// <summary>
    /// Zamyka picker przepisów.
    /// </summary>
    [RelayCommand]
    private void CloseRecipePicker()
    {
        IsRecipePickerVisible = false;
        IsCategoryStepVisible = false;
        IsRecipeStepVisible = false;
        SelectedCategory = null;
        _selectedDayForAdding = null;
    }

    /// <summary>
    /// Wybiera kategorię i przechodzi do listy przepisów.
    /// </summary>
    [RelayCommand]
    private void SelectCategory(CategoryLocal category)
    {
        SelectedCategory = category;
        RecipeSearchQuery = string.Empty;
        FilterRecipesByCategory();
        IsCategoryStepVisible = false;
        IsRecipeStepVisible = true;
    }

    /// <summary>
    /// Wraca do listy kategorii.
    /// </summary>
    [RelayCommand]
    private void BackToCategories()
    {
        SelectedCategory = null;
        RecipeSearchQuery = string.Empty;
        IsRecipeStepVisible = false;
        IsCategoryStepVisible = true;
    }

    /// <summary>
    /// Dodaje wybrany przepis do planu.
    /// </summary>
    [RelayCommand]
    private async Task SelectRecipeAsync(RecipeLocal recipe)
    {
        if (_selectedDayForAdding is null) return;

        try
        {
            var mealPlan = new MealPlanLocal
            {
                Id = Guid.NewGuid(),
                Date = _selectedDayForAdding.Date,
                RecipeId = recipe.Id
            };

            await _database.SaveMealPlanAsync(mealPlan);
            IsRecipePickerVisible = false;
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding meal to plan");
            await Shell.Current.DisplayAlertAsync("Błąd", "Nie udało się dodać posiłku", "OK");
        }
    }

    /// <summary>
    /// Usuwa posiłek z planu.
    /// </summary>
    [RelayCommand]
    private async Task RemoveMealAsync(MealItem meal)
    {
        var confirm = await Shell.Current.DisplayAlertAsync(
       "Usuń posiłek",
       $"Czy na pewno chcesz usunąć \"{meal.Recipe?.Title}\" z planu?",
   "Usuń", "Anuluj");

        if (!confirm) return;

        try
        {
            await _database.DeleteMealPlanAsync(meal.Id);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing meal from plan");
            await Shell.Current.DisplayAlertAsync("Błąd", "Nie udało się usunąć posiłku", "OK");
        }
    }

    /// <summary>
    /// Nawiguje do szczegółów przepisu.
    /// </summary>
    [RelayCommand]
    private async Task GoToRecipeDetailAsync(MealItem meal)
    {
        if (meal.Recipe is null) return;

        await Shell.Current.GoToAsync($"RecipeDetailPage?recipeId={meal.Recipe.Id}");
    }

    private static string GetPolishDayName(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday => "Poniedziałek",
        DayOfWeek.Tuesday => "Wtorek",
        DayOfWeek.Wednesday => "Środa",
        DayOfWeek.Thursday => "Czwartek",
        DayOfWeek.Friday => "Piątek",
        DayOfWeek.Saturday => "Sobota",
        DayOfWeek.Sunday => "Niedziela",
        _ => string.Empty
    };
}

/// <summary>
/// Reprezentuje plan na jeden dzień.
/// </summary>
public partial class DayPlanItem : ObservableObject
{
    public DateTime Date { get; set; }
    public string DayName { get; set; } = string.Empty;
    public string DateText { get; set; } = string.Empty;
    public bool IsToday { get; set; }
    public bool IsPastDay { get; set; }

    public ObservableCollection<MealItem> Meals { get; set; } = [];
}

/// <summary>
/// Reprezentuje pojedynczy posiłek w planie.
/// </summary>
public class MealItem
{
    public Guid Id { get; set; }
    public RecipeLocal? Recipe { get; set; }
    public string? Notes { get; set; }
}
