namespace ReceptyOks.ViewModels;

/// <summary>
/// Slot datowy na timeline tygodnia — odpowiednik HourSlot, ale dla dat zamiast godzin.
/// </summary>
public class DateSlot
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool IsToday { get; set; }
    public bool IsPastDay { get; set; }
    public bool IsOccupied { get; set; }

    /// <summary>
    /// Podsumowanie posiłków na dany dzień (np. tytuł przepisu lub "3 posiłki").
    /// </summary>
    public string? MealSummary { get; set; }

    /// <summary>
    /// Liczba posiłków w formacie tekstowym.
    /// </summary>
    public string? MealCountLabel { get; set; }

    /// <summary>
    /// Pierwszy posiłek na dany dzień — używany do wyświetlenia szczegółów i usuwania.
    /// </summary>
    public MealItem? FirstMeal { get; set; }

    /// <summary>
    /// Wszystkie posiłki przypisane do tego dnia.
    /// </summary>
    public ObservableCollection<MealItem> Meals { get; set; } = [];
}
