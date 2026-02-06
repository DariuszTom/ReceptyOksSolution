using ReceptyOks.Data;

namespace ReceptyOks.ViewModels;

/// <summary>
/// Pojedynczy posiłek w planie z pozycją na timeline.
/// </summary>
public class MealItem
{
    public Guid Id { get; set; }
    public RecipeLocal? Recipe { get; set; }
    public string? Notes { get; set; }
    public int StartHour { get; set; }
    public int DurationMinutes { get; set; }

    /// <summary>
    /// Offset od góry timeline w pikselach (do pozycjonowania).
    /// </summary>
    public double TopOffset { get; set; }

    /// <summary>
    /// Wysokość bloku w pikselach.
    /// </summary>
    public double Height { get; set; }

    /// <summary>
    /// Tekst czasu do wyświetlenia (np. "12:00 – 13:30").
    /// </summary>
    public string TimeRangeText
    {
        get
        {
            var start = TimeSpan.FromHours(StartHour);
            var end = start.Add(TimeSpan.FromMinutes(DurationMinutes));
            return $"{start:hh\\:mm} – {end:hh\\:mm}";
        }
    }
}
