using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace HomeSeeker.Scrapers;

/// <summary>
/// Builds the plain-text "details" block (parameters + description) that is passed to the AI.
/// Scraped content is untrusted: HTML is stripped, prompt delimiters are neutralised and the
/// text is length-capped so a listing can't blow up the prompt or fake the data block's end.
/// </summary>
internal static partial class ListingDetailsText
{
    internal const int MaxLength = 4000;
    private const int MaxDescriptionLength = 3000;

    internal static string? Build(IEnumerable<(string Name, string Value)> parameters, string? descriptionHtml)
    {
        var sb = new StringBuilder();

        foreach (var (name, value) in parameters)
        {
            var n = Clean(name);
            var v = Clean(value);
            if (n.Length > 0 && v.Length > 0)
                sb.Append("- ").Append(n).Append(": ").AppendLine(v);
        }

        var description = HtmlToText(descriptionHtml);
        if (description.Length > 0)
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.AppendLine("Opis:");
            sb.AppendLine(Truncate(description, MaxDescriptionLength));
        }

        var result = Truncate(sb.ToString().Trim(), MaxLength);
        return result.Length == 0 ? null : result;
    }

    internal static string HtmlToText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var withBreaks = BlockTagRegex().Replace(html, "\n");
        var doc = new HtmlDocument();
        doc.LoadHtml(withBreaks);
        var text = HtmlEntity.DeEntitize(doc.DocumentNode.InnerText) ?? string.Empty;

        var lines = text.Split('\n')
            .Select(Clean)
            .Where(l => l.Length > 0);
        return string.Join('\n', lines);
    }

    /// <summary>
    /// Single-line cleanup: collapses whitespace and defuses "---" so scraped text can't
    /// imitate the "--- KONIEC DANYCH OGŁOSZENIA ---" delimiter.
    /// </summary>
    private static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        var collapsed = WhitespaceRegex().Replace(value, " ").Trim();
        return DashRunRegex().Replace(collapsed, "–");
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max].TrimEnd() + "…";

    [GeneratedRegex(@"<\s*(br|/p|/li|/div|/h\d|/tr)\s*/?\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockTagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"-{3,}")]
    private static partial Regex DashRunRegex();
}
