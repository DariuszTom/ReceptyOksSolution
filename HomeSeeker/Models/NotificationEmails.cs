namespace HomeSeeker.Models;

/// <summary>
/// Helpers for the household's notification recipient list. The list is stored
/// in <see cref="SearchProfile.NotificationEmail"/> as a single string so that
/// several people can receive the report without a schema change.
/// </summary>
public static class NotificationEmails
{
    public const int MaxLength = 200;
    public const int MaxRecipients = 5;

    private static readonly char[] Separators = [',', ';'];

    /// <summary>
    /// Splits a comma/semicolon separated list into trimmed, non-empty,
    /// case-insensitively distinct addresses, preserving order.
    /// </summary>
    public static IReadOnlyList<string> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        return value
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Normalizes a recipient list to the canonical ", " separated form.
    /// </summary>
    public static string Normalize(string? value) => string.Join(", ", Parse(value));
}
