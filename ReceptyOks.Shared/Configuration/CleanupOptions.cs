namespace ReceptyOks.Shared.Configuration;

/// <summary>
/// Timing options shared by background cleanup services (log cleanup, shopping list cleanup, etc.).
/// </summary>
public sealed record CleanupOptions
{
    /// <summary>How often the cleanup job runs.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromHours(24);

    /// <summary>Delay after app startup before the first run.</summary>
    public TimeSpan StartupDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Items older than this are eligible for deletion.</summary>
    public TimeSpan MaxAge { get; init; } = TimeSpan.FromDays(7);
}
