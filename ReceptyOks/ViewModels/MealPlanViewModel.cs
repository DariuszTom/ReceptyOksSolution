using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ReceptyOks.Data;
using System.Collections.ObjectModel;

namespace ReceptyOks.ViewModels;

/// <summary>
/// ViewModel dla strony planowania menu na tydzień z timeline (0–24h).
/// </summary>
public partial class MealPlanViewModel : ObservableObject
{
    private const int MinDurationMinutes = 30;
    private const int TimelineStartHour = 6;
    private const int TimelineEndHour = 23;
    private const double HourSlotHeight = 60.0;

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

    // Recipe picker
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

    // Timeline picker state
    [ObservableProperty]
    private string selectedTimeSlotText = string.Empty;

    private DayPlanItem? _selectedDayForAdding;
    private int _selectedStartHour;

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

                var dayMeals = mealPlansWithRecipes
                    .Where(mp => mp.MealPlan.Date.Date == date.Date)
                    .OrderBy(mp => mp.MealPlan.StartHour)
                    .ToList();

                foreach (var item in dayMeals)
                {
                    var duration = Math.Max(item.MealPlan.DurationMinutes, MinDurationMinutes);
                    dayItem.Meals.Add(new MealItem
                    {
                        Id = item.MealPlan.Id,
                        Recipe = item.Recipe,
                        Notes = item.MealPlan.Notes,
                        StartHour = item.MealPlan.StartHour,
                        DurationMinutes = duration,
                        TopOffset = (item.MealPlan.StartHour - TimelineStartHour) * HourSlotHeight,
                        Height = duration / 60.0 * HourSlotHeight
                    });
                }

                // Build hour slots
                for (var h = TimelineStartHour; h < TimelineEndHour; h++)
                {
                    var meal = dayItem.Meals.FirstOrDefault(m =>
                        h >= m.StartHour && h < m.StartHour + (m.DurationMinutes / 60.0));

                    dayItem.HourSlots.Add(new HourSlot
                    {
                        Hour = h,
                        Label = $"{h:00}:00",
                        IsOccupied = meal is not null,
                        IsStartHour = meal is not null && meal.StartHour == h,
                        MealTitle = meal?.Recipe?.Title,
                        MealTimeRange = meal?.TimeRangeText,
                        MealRef = meal
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

    [RelayCommand]
    private async Task PreviousWeekAsync()
    {
        CurrentWeekStart = CurrentWeekStart.AddDays(-7);
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task NextWeekAsync()
    {
        CurrentWeekStart = CurrentWeekStart.AddDays(7);
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task GoToTodayAsync()
    {
        CurrentWeekStart = GetStartOfWeek(DateTime.Today);
        await LoadDataAsync();
    }

    /// <summary>
    /// Użytkownik dotknął slot godzinowy na timeline.
    /// </summary>
    public void OnTimeSlotTapped(DayPlanItem day, int hour)
    {
        if (day.IsPastDay) return;

        // Check if slot is already occupied
        var isOccupied = day.Meals.Any(m =>
            hour >= m.StartHour && hour < m.StartHour + (m.DurationMinutes / 60.0));

        if (isOccupied) return;

        _selectedDayForAdding = day;
        _selectedStartHour = hour;
        SelectedTimeSlotText = $"{day.DayName} {day.DateText}, godz. {hour:00}:00";

        SelectedCategory = null;
        RecipeSearchQuery = string.Empty;
        IsCategoryStepVisible = true;
        IsRecipeStepVisible = false;
        IsRecipePickerVisible = true;
    }

    [RelayCommand]
    private void CloseRecipePicker()
    {
        IsRecipePickerVisible = false;
        IsCategoryStepVisible = false;
        IsRecipeStepVisible = false;
        SelectedCategory = null;
        _selectedDayForAdding = null;
    }

    [RelayCommand]
    private void SelectCategory(CategoryLocal category)
    {
        SelectedCategory = category;
        RecipeSearchQuery = string.Empty;
        FilterRecipesByCategory();
        IsCategoryStepVisible = false;
        IsRecipeStepVisible = true;
    }

    [RelayCommand]
    private void BackToCategories()
    {
        SelectedCategory = null;
        RecipeSearchQuery = string.Empty;
        IsRecipeStepVisible = false;
        IsCategoryStepVisible = true;
    }

    /// <summary>
    /// Dodaje wybrany przepis do planu z godziną i czasem trwania.
    /// </summary>
    [RelayCommand]
    private async Task SelectRecipeAsync(RecipeLocal recipe)
    {
        if (_selectedDayForAdding is null) return;

        try
        {
            var totalTime = recipe.PreparationTimeMinutes + recipe.CookingTimeMinutes;
            var duration = Math.Max(totalTime, MinDurationMinutes);

            // Validate no overlap
            var endHour = _selectedStartHour + (duration / 60.0);
            var hasOverlap = _selectedDayForAdding.Meals.Any(m =>
            {
                var existingEnd = m.StartHour + (m.DurationMinutes / 60.0);
                return _selectedStartHour < existingEnd && endHour > m.StartHour;
            });

            if (hasOverlap)
            {
                await Shell.Current.DisplayAlertAsync("Konflikt", "Wybrany slot czasowy nakłada się z innym posiłkiem.", "OK");
                return;
            }

            if (endHour > TimelineEndHour + 1)
            {
                await Shell.Current.DisplayAlertAsync("Za późno", "Posiłek wykracza poza timeline. Wybierz wcześniejszą godzinę.", "OK");
                return;
            }

            var mealPlan = new MealPlanLocal
            {
                Id = Guid.NewGuid(),
                Date = _selectedDayForAdding.Date,
                StartHour = _selectedStartHour,
                DurationMinutes = duration,
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

    [RelayCommand]
    private async Task GoToRecipeDetailAsync(MealItem meal)
    {
        if (meal.Recipe is null) return;
        await Shell.Current.GoToAsync($"{nameof(Views.RecipeDetailPage)}?id={meal.Recipe.Id}");
    }

    [RelayCommand]
    private async Task GenerateShoppingListAsync()
    {
        // TODO: Implement shopping list generation from current week's meal plan
        var snackbar = Snackbar.Make("Funkcja listy zakupów w przygotowaniu",
            duration: TimeSpan.FromSeconds(3));
        await snackbar.Show();
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
