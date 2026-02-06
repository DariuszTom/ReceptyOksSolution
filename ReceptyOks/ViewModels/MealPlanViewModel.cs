using AsyncAwaitBestPractices;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ReceptyOks.Data;
using ReceptyOks.Services;
using ReceptyOks.Shared.AI;
using ReceptyOks.Shared.Misc;
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
    private readonly AgentToolsRegistrar _toolsRegistrar;
    private AiAgent _agent;

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

    // Agent readiness state
    [ObservableProperty]
    private bool isAgentReady;

    [ObservableProperty]
    private bool isAgentInitializing;

    // Timeline picker state
    [ObservableProperty]
    private string selectedTimeSlotText = string.Empty;

    private DayPlanItem? _selectedDayForAdding;
    private int _selectedStartHour;
    private bool _categoriesAndRecipesLoaded;

    public MealPlanViewModel(LocalDatabase database, ILogger<MealPlanViewModel> logger, TokenProviderService tokenProvider)
    {
        _database = database;
        _logger = logger;
        _toolsRegistrar = new AgentToolsRegistrar(database, Serilog.Log.Logger);
        CurrentWeekStart = DateTime.Today.GetStartOfWeek();
        InitlizeShoppingListAgent(tokenProvider).SafeFireAndForget(ConfigureAwaitOptions.SuppressThrowing);
    }
    public async Task InitlizeShoppingListAgent(TokenProviderService tokenProvider)
    {
        if (_agent is not null) return;

        IsAgentInitializing = true;
        IsAgentReady = false;

        try
        {
            // Get API token from backend
            var tokenResponse = await tokenProvider.GetTokenAsync(CancellationToken.None).ConfigureAwait(false);
            if (tokenResponse is null || string.IsNullOrWhiteSpace(tokenResponse.Token))
            {
                throw new InvalidOperationException("Failed to retrieve API token from backend");
            }

            var tokenBytes = System.Text.Encoding.UTF8.GetBytes(tokenResponse.Token);

            var settings = new AnthropicSettings();

            using (var anthritopicAgent = new AnthropicAgent(settings, tokenBytes))
            {
                _agent = new AiAgent(anthritopicAgent.GetAgent(), settings.SystemPromtShopingList);
            }

            // Register tools
            _toolsRegistrar.RegisterToolsForShopingList(_agent);
            IsAgentReady = true;
        }
        catch
        {
            _logger.LogError("Failed to initialize AI agent for shopping list generation");
            IsAgentReady = false;
        }
        finally
        {
            IsAgentInitializing = false;
        }
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

            // Cache categories and recipes — they rarely change during a session
            if (!_categoriesAndRecipesLoaded)
            {
                var categories = await _database.GetCategoriesAsync();
                AvailableCategories = new ObservableCollection<CategoryLocal>(categories);

                var recipes = await _database.GetRecipesAsync();
                AvailableRecipes = new ObservableCollection<RecipeLocal>(recipes);
                FilterRecipes();
                _categoriesAndRecipesLoaded = true;
            }

            var endOfWeek = CurrentWeekStart.AddDays(6);
            var mealPlansWithRecipes = await _database.GetMealPlansWithRecipesAsync(CurrentWeekStart, endOfWeek);

            // Group meal plans by date once for O(1) lookup per day
            var mealsByDate = mealPlansWithRecipes
                .GroupBy(mp => mp.MealPlan.Date.Date)
                .ToDictionary(g => g.Key, g => g.OrderBy(mp => mp.MealPlan.StartHour).ToList());

            var today = DateTime.Today;
            var days = new ObservableCollection<DayPlanItem>();
            for (var i = 0; i < 7; i++)
            {
                var date = CurrentWeekStart.AddDays(i);
                var isToday = date.Date == today;
                var dayItem = new DayPlanItem
                {
                    Date = date,
                    DayName = date.DayOfWeek.GetPolishDayName(),
                    DateText = date.ToString("dd.MM"),
                    IsToday = isToday,
                    IsPastDay = date.Date < today,
                    IsExpanded = isToday
                };

                if (mealsByDate.TryGetValue(date.Date, out var dayMeals))
                {
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
                }

                BuildHourSlots(dayItem);
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

    private static void BuildHourSlots(DayPlanItem dayItem)
    {
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
    }

    /// <summary>
    /// Invalidates cached categories/recipes so the next LoadDataAsync reloads them.
    /// </summary>
    public void InvalidateCatalogCache() => _categoriesAndRecipesLoaded = false;

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
        CurrentWeekStart = DateTime.Today.GetStartOfWeek();
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
        if (!IsAgentReady || _agent is null)
        {
            var message = IsAgentInitializing
                ? "Agent AI jest w trakcie inicjalizacji. Spróbuj ponownie za chwilę."
                : "Agent AI nie jest gotowy. Sprawdź połączenie i spróbuj ponownie.";
            var snackbar = Snackbar.Make(message,
                duration: TimeSpan.FromSeconds(3),
                visualOptions: new SnackbarOptions
                {
                    BackgroundColor = Colors.Gold,
                    TextColor = Colors.Black
                });
            await snackbar.Show();
            return;
        }

        // TODO: Implement shopping list generation from current week's meal plan
        var todoSnackbar = Snackbar.Make("Funkcja listy zakupów w przygotowaniu",
            duration: TimeSpan.FromSeconds(3));
        await todoSnackbar.Show();
    }
}
