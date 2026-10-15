using System.Text.Json;

namespace HomeSeeker.Scrapers
{
    internal static class ScraperHelper
    {
        internal static string NormalizeCity(string city)
        {
            return city.ToLowerInvariant()
                .Replace("ą", "a")
                .Replace("ć", "c")
                .Replace("ę", "e")
                .Replace("ł", "l")
                .Replace("ń", "n")
                .Replace("ó", "o")
                .Replace("ś", "s")
                .Replace("ź", "z")
                .Replace("ż", "z")
                .Replace(" ", "-");
        }

        /// <summary>Codes for "minRooms or more"; the last code is open-ended and always included.</summary>
        internal static IEnumerable<string> RoomCodesFrom(string[] codes, int minRooms) =>
            codes.Skip(Math.Min(minRooms, codes.Length) - 1);

        /// <summary>Exact room count for a code, or null when unknown or open-ended (last code).</summary>
        internal static int? RoomCountFromCode(string[] codes, string? code)
        {
            var index = Array.FindIndex(codes, c => c.Equals(code, StringComparison.OrdinalIgnoreCase));
            return index >= 0 && index < codes.Length - 1 ? index + 1 : null;
        }

        internal static string? GetString(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
                ? prop.GetString()
                : null;

        internal static string? GetNestedName(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Object
                ? GetString(prop, "name")
                : null;
    }
}
