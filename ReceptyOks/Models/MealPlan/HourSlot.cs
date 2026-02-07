namespace ReceptyOks.ViewModels;

/// <summary>
/// Slot godzinowy na timeline.
/// </summary>
public class HourSlot
{
    public int Hour { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool IsOccupied { get; set; }

    /// <summary>
    /// True only for the first hour of the meal block.
    /// </summary>
    public bool IsStartHour { get; set; }

    public string? MealTitle { get; set; }
    public string? MealTimeRange { get; set; }
    public MealItem? MealRef { get; set; }
}
