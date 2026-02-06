using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace ReceptyOks.ViewModels;

/// <summary>
/// Reprezentuje plan na jeden dzień z timeline.
/// </summary>
public partial class DayPlanItem : ObservableObject
{
    public DateTime Date { get; set; }
    public string DayName { get; set; } = string.Empty;
    public string DateText { get; set; } = string.Empty;
    public bool IsToday { get; set; }
    public bool IsPastDay { get; set; }

    [ObservableProperty]
    private bool isExpanded;

    /// <summary>
    /// Podsumowanie posiłków do wyświetlenia gdy timeline jest zwinięty.
    /// </summary>
    public string MealCountText => Meals.Count switch
    {
        0 => "Brak posiłków",
        1 => "1 posiłek",
        _ => $"{Meals.Count} posiłki"
    };

    public ObservableCollection<MealItem> Meals { get; set; } = [];
    public ObservableCollection<HourSlot> HourSlots { get; set; } = [];
}
