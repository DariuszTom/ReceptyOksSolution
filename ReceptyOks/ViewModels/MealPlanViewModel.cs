using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ReceptyOks.Data;
using System.Collections.ObjectModel;
using System.Globalization;

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
    private string recipeSearchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<RecipeLocal> filteredRecipes = [];

    private DayPlanItem? _selectedDayForAdding;
    private MealType _selectedMealTypeForAdding;

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
    if (string.IsNullOrWhiteSpace(RecipeSearchQuery))
   {
        FilteredRecipes = new ObservableCollection<RecipeLocal>(AvailableRecipes);
        }
    else
        {
  FilteredRecipes = new ObservableCollection<RecipeLocal>(
          AvailableRecipes.Where(r =>
            r.Title.Contains(RecipeSearchQuery, StringComparison.OrdinalIgnoreCase) ||
   r.Description.Contains(RecipeSearchQuery, StringComparison.OrdinalIgnoreCase)));
        }
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
IsToday = date.Date == DateTime.Today
    };

        var dayMeals = mealPlansWithRecipes.Where(mp => mp.MealPlan.Date.Date == date.Date).ToList();

       foreach (var item in dayMeals)
       {
       var mealItem = new MealItem
           {
                   Id = item.MealPlan.Id,
        MealType = (MealType)item.MealPlan.MealType,
          Recipe = item.Recipe,
     Notes = item.MealPlan.Notes
 };

          switch ((MealType)item.MealPlan.MealType)
  {
            case MealType.Breakfast:
    dayItem.BreakfastMeals.Add(mealItem);
             break;
         case MealType.Lunch:
   dayItem.LunchMeals.Add(mealItem);
  break;
      case MealType.Dinner:
 dayItem.DinnerMeals.Add(mealItem);
            break;
        case MealType.Snack:
  dayItem.SnackMeals.Add(mealItem);
       break;
         }
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
    /// Otwiera picker do dodania przepisu.
    /// </summary>
    [RelayCommand]
    private void OpenRecipePicker(AddMealParameter parameter)
    {
        _selectedDayForAdding = parameter.DayPlan;
 _selectedMealTypeForAdding = parameter.MealType;
        RecipeSearchQuery = string.Empty;
        FilterRecipes();
        IsRecipePickerVisible = true;
 }

    /// <summary>
    /// Zamyka picker przepisów.
    /// </summary>
    [RelayCommand]
    private void CloseRecipePicker()
    {
        IsRecipePickerVisible = false;
    _selectedDayForAdding = null;
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
     MealType = (int)_selectedMealTypeForAdding,
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

    public ObservableCollection<MealItem> BreakfastMeals { get; set; } = [];
    public ObservableCollection<MealItem> LunchMeals { get; set; } = [];
    public ObservableCollection<MealItem> DinnerMeals { get; set; } = [];
public ObservableCollection<MealItem> SnackMeals { get; set; } = [];

    public bool HasBreakfast => BreakfastMeals.Count > 0;
    public bool HasLunch => LunchMeals.Count > 0;
    public bool HasDinner => DinnerMeals.Count > 0;
    public bool HasSnack => SnackMeals.Count > 0;
}

/// <summary>
/// Reprezentuje pojedynczy posiłek w planie.
/// </summary>
public class MealItem
{
    public Guid Id { get; set; }
    public MealType MealType { get; set; }
    public RecipeLocal? Recipe { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Parametr do komendy dodawania posiłku. Tworzony w code-behind.
/// </summary>
public class AddMealParameter
{
    public DayPlanItem? DayPlan { get; set; }
    public MealType MealType { get; set; }

    public AddMealParameter()
    {
    }

    public AddMealParameter(DayPlanItem dayPlan, MealType mealType)
    {
        DayPlan = dayPlan;
        MealType = mealType;
    }
}
