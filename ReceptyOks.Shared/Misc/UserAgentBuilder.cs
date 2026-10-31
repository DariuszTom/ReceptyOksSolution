namespace ReceptyOks.Shared.Misc;

/// <summary>
/// Single source of User-Agent values for the solution: <see cref="App"/> for public APIs that require
/// an honest client identification, and random browser User-Agents for fetching regular web pages.
/// </summary>
public static class UserAgentBuilder
{
    /// <summary>Honest app identification for public APIs (Nominatim usage policy, OSM tiles, etc.).</summary>
    public const string App = "ReceptyOks/1.0 (+https://github.com/DariuszTom/ReceptyOksSolution)";

    private static readonly string[] UserAgents =
    [
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36 Edg/141.0.0.0",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:144.0) Gecko/20100101 Firefox/144.0",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:143.0) Gecko/20100101 Firefox/143.0",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Safari/605.1.15",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 15.6; rv:144.0) Gecko/20100101 Firefox/144.0",
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36",
        "Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:144.0) Gecko/20100101 Firefox/144.0",
    ];

    /// <summary>All User-Agents the builder can return.</summary>
    public static IReadOnlyList<string> All => UserAgents;

    /// <summary>Returns a random User-Agent from <see cref="All"/>.</summary>
    public static string Next() => UserAgents[Random.Shared.Next(UserAgents.Length)];

    /// <summary>Returns a random User-Agent different from <paramref name="previous"/>.</summary>
    public static string Next(string? previous)
    {
        string next;
        do
        {
            next = Next();
        }
        while (next == previous);

        return next;
    }
}
