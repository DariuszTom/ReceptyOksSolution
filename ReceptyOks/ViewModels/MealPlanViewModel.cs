using AsyncAwaitBestPractices;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Shapes;
using ReceptyOks.Data;
using ReceptyOks.Services;
using ReceptyOks.Shared.AI;
using ReceptyOks.Shared.Misc;
using ReceptyOks.Shared.Models;
using ReceptyOks.Views;
using System.Collections.ObjectModel;

namespace ReceptyOks.ViewModels;

/// <summary>
/// ViewModel dla strony planowania menu na tydzień z timeline datowym.
/// </summary>
public partial class MealPlanViewModel : ObservableObject
{
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

    /// <summary>
    /// Sloty datowe na timeline tygodnia (7 dni).
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DateSlot> dateSlots = [];

    /// <summary>
    /// Czy timeline tygodnia jest rozwinięty.
    /// </summary>
    [ObservableProperty]
    private bool isWeekExpanded = true;

    /// <summary>
    /// Podsumowanie posiłków na cały tydzień.
    /// </summary>
    [ObservableProperty]
    private string weekMealCountText = "Brak posiłków";

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

    // Shopping list state
    [ObservableProperty]
    private string generatedShoppingList = string.Empty;

    [ObservableProperty]
    private bool isGeneratingShoppingList;

    [ObservableProperty]
    private ObservableCollection<ShoppingListItemDto> generatedShoppingListItems = [];

    // Date slot picker state
    [ObservableProperty]
    private string selectedTimeSlotText = string.Empty;

    private DateSlot? _selectedDateSlotForAdding;
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

            var categoryLookup = AvailableCategories.ToDictionary(c => c.Id, c => c.Name);

            var today = DateTime.Today;
            var days = new ObservableCollection<DayPlanItem>();
            var slots = new ObservableCollection<DateSlot>();
            var totalMeals = 0;

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
                        string? catName = null;
                        if (item.Recipe?.CategoryId is { } catId)
                        {
                            categoryLookup.TryGetValue(catId, out catName);
                        }

                        dayItem.Meals.Add(new MealItem
                        {
                            Id = item.MealPlan.Id,
                            Recipe = item.Recipe,
                            Notes = item.MealPlan.Notes,
                            StartHour = item.MealPlan.StartHour,
                            DurationMinutes = item.MealPlan.DurationMinutes,
                            CategoryName = catName
                        });
                    }
                }

                totalMeals += dayItem.Meals.Count;
                slots.Add(BuildDateSlot(dayItem));
                days.Add(dayItem);
            }

            WeekDays = days;
            DateSlots = slots;
            WeekMealCountText = totalMeals switch
            {
                0 => "Brak posiłków",
                1 => "1 posiłek",
                _ => $"{totalMeals} posiłki"
            };
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

    private static DateSlot BuildDateSlot(DayPlanItem dayItem)
    {
        var hasMeals = dayItem.Meals.Count > 0;
        var firstMeal = hasMeals ? dayItem.Meals[0] : null;

        var summary = dayItem.Meals.Count switch
        {
            0 => null,
            1 => firstMeal?.Recipe?.Title,
            _ => firstMeal?.Recipe?.Title
        };

        var countLabel = dayItem.Meals.Count switch
        {
            0 => null,
            1 => "1 posiłek",
            _ => $"{dayItem.Meals.Count} posiłki"
        };

        return new DateSlot
        {
            Date = dayItem.Date,
            Label = $"{dayItem.DayName}  {dayItem.DateText}",
            IsToday = dayItem.IsToday,
            IsPastDay = dayItem.IsPastDay,
            IsOccupied = hasMeals,
            MealSummary = summary,
            MealCountLabel = countLabel,
            FirstMeal = firstMeal,
            Meals = dayItem.Meals
        };
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
    /// Użytkownik dotknął slot datowy na timeline tygodnia.
    /// </summary>
    public void OnDateSlotTapped(DateSlot dateSlot)
    {
        if (dateSlot.IsPastDay) return;

        _selectedDateSlotForAdding = dateSlot;
        SelectedTimeSlotText = dateSlot.Label;

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
        _selectedDateSlotForAdding = null;
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
    /// Dodaje wybrany przepis do planu na wybrany dzień.
    /// </summary>
    [RelayCommand]
    private async Task SelectRecipeAsync(RecipeLocal recipe)
    {
        if (_selectedDateSlotForAdding is null) return;

        try
        {
            var totalTime = recipe.PreparationTimeMinutes + recipe.CookingTimeMinutes;

            var mealPlan = new MealPlanLocal
            {
                Id = Guid.NewGuid(),
                Date = _selectedDateSlotForAdding.Date,
                StartHour = 0,
                DurationMinutes = Math.Max(totalTime, 1),
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
            await SnackBarHelper.ShowWarningSnackbarAsync(message);

            return;
        }

        // Collect unique recipe IDs from the current week
        var recipesInPlan = WeekDays
            .SelectMany(d => d.Meals)
            .Where(m => m.Recipe is not null)
            .Select(m => (m.Recipe!.Id, m.Recipe.Title))
            .ToList();

        if (recipesInPlan.Count == 0)
        {
            await SnackBarHelper.ShowInfoSnackbarAsync("Brak przepisów w planie na ten tydzień.");
            return;
        }

        try
        {
            IsGeneratingShoppingList = true;

            var recipeList = string.Join("\n", recipesInPlan.Select(r => $"- {r.Title} (ID: {r.Id})"));
            var prompt = $"Wygeneruj listę zakupów dla następujących przepisów zaplanowanych na tydzień " +
                $"{WeekRangeText}:\n{recipeList}\n\nUżyj narzędzia get_all_ingredients_for_recipes aby pobrać składniki dla tych przepisów." +
                $" Zsumuj duplikaty i podaj finalną listę zakupów w formacie JSON.";

            var structuredResult = await _agent.ChatAsync<ShoppingListAiResponse>(prompt).ConfigureAwait(false);

            if (structuredResult is not null)
            {
                GeneratedShoppingList = structuredResult.Summary;
                GeneratedShoppingListItems = new ObservableCollection<ShoppingListItemDto>(structuredResult.Items);
            }
            else
            {
                // Fallback to raw text response if JSON parsing failed
                var rawResult = await _agent.ChatAsync(prompt).ConfigureAwait(false);
                GeneratedShoppingList = rawResult;
                GeneratedShoppingListItems = [];
            }

            await MainThread.InvokeOnMainThreadAsync(async () =>
           {
               var popup = new ShopingListPopup(GeneratedShoppingList, GeneratedShoppingListItems);
               var options = new PopupOptions
               {
                   CanBeDismissedByTappingOutsideOfPopup = true,
                   Shape = new RoundRectangle
                   {
                       CornerRadius = new CornerRadius(16)
                   }
               };
               var page = Shell.Current.CurrentPage;
               await page.ShowPopupAsync(popup, options, CancellationToken.None);
           });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating shopping list");
            await SnackBarHelper.ShowErrorSnackbarAsync("Nie udało się wygenerować listy zakupów.");
        }
        finally
        {
            IsGeneratingShoppingList = false;
        }
    }
}
