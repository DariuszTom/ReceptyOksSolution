using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.AI;

namespace ReceptyOks.Shared.AI;

/// <summary>
/// Provides web browsing capabilities for AI agents.
/// Allows fetching web page content and performing web searches.
/// </summary>
public sealed partial class WebBrowsingTool : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly int _maxContentLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebBrowsingTool"/> class.
    /// </summary>
    /// <param name="httpClient">Optional HttpClient instance. If not provided, a new one will be created.</param>
    /// <param name="maxContentLength">Maximum content length to return (default: 50000 characters).</param>
    public WebBrowsingTool(HttpClient? httpClient = null, int maxContentLength = 50000)
    {
        if (httpClient is null)
        {
            _httpClient = new HttpClient();
            _ownsHttpClient = true;
        }
        else
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; AIAgent/1.0)");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
        _httpClient.Timeout = TimeSpan.FromSeconds(30);

        _maxContentLength = maxContentLength;
    }

    /// <summary>
    /// Registers all web browsing tools with the AI agent.
    /// </summary>
    /// <param name="agent">The agent to register tools with.</param>
    public void RegisterTools(AiAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        agent.AddTool(AIFunctionFactory.Create(
            FetchWebPageAsync,
            "fetch_web_page",
            "Fetches and extracts text content from a web page URL. Returns the main text content without HTML tags. Parameter: url - the full URL of the web page to fetch (must start with http:// or https://)."));

        agent.AddTool(AIFunctionFactory.Create(
            SearchWebAsync,
            "search_web",
            "Searches the web using DuckDuckGo and returns search results. Parameter: query - the search query text."));
    }

    /// <summary>
    /// Fetches content from a web page and extracts readable text.
    /// </summary>
    /// <param name="url">The URL of the web page to fetch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The extracted text content from the web page.</returns>
    public async Task<string> FetchWebPageAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "Error: URL cannot be empty.";
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return "Error: Invalid URL. Please provide a valid HTTP or HTTPS URL.";
        }

        try
        {
            using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return $"Error: Failed to fetch page. Status code: {(int)response.StatusCode} {response.ReasonPhrase}";
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "text/html";
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(content))
            {
                return "Error: The page returned empty content.";
            }

            var extractedText = contentType.Contains("html", StringComparison.OrdinalIgnoreCase)
                ? ExtractTextFromHtml(content)
                : content;

            if (extractedText.Length > _maxContentLength)
            {
                extractedText = string.Concat(extractedText.AsSpan(0, _maxContentLength), "\n\n[Content truncated due to length...]");
            }

            return $"Content from {url}:\n\n{extractedText}";
        }
        catch (HttpRequestException ex)
        {
            return $"Error: Network error while fetching page: {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            return "Error: Request timed out while fetching the page.";
        }
        catch (Exception ex)
        {
            return $"Error: Unexpected error while fetching page: {ex.Message}";
        }
    }

    /// <summary>
    /// Searches the web using DuckDuckGo HTML interface.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results with titles and URLs.</returns>
    public async Task<string> SearchWebAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "Error: Search query cannot be empty.";
        }

        try
        {
            var encodedQuery = HttpUtility.UrlEncode(query);
            var searchUrl = $"https://html.duckduckgo.com/html/?q={encodedQuery}";

            using var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return $"Error: Search failed with status code: {(int)response.StatusCode}";
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var results = ExtractSearchResults(html);

            if (results.Count == 0)
            {
                return $"No search results found for: {query}";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Search results for: {query}");
            sb.AppendLine();

            for (var i = 0; i < results.Count && i < 10; i++)
            {
                var (title, resultUrl, snippet) = results[i];
                sb.AppendLine($"{i + 1}. {title}");
                sb.AppendLine($"   URL: {resultUrl}");
                if (!string.IsNullOrWhiteSpace(snippet))
                {
                    sb.AppendLine($"   {snippet}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (HttpRequestException ex)
        {
            return $"Error: Network error during search: {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            return "Error: Search request timed out.";
        }
        catch (Exception ex)
        {
            return $"Error: Unexpected error during search: {ex.Message}";
        }
    }

    /// <summary>
    /// Extracts readable text from HTML content.
    /// </summary>
    private string ExtractTextFromHtml(string html)
    {
        // Remove script and style elements
        var cleaned = ScriptStyleRegex().Replace(html, " ");

        // Remove HTML comments
        cleaned = CommentRegex().Replace(cleaned, " ");

        // Remove all HTML tags
        cleaned = TagRegex().Replace(cleaned, " ");

        // Decode HTML entities
        cleaned = HttpUtility.HtmlDecode(cleaned);

        // Normalize whitespace
        cleaned = WhitespaceRegex().Replace(cleaned, " ");

        // Replace multiple newlines with double newline
        cleaned = NewlineRegex().Replace(cleaned.Trim(), "\n\n");

        return cleaned;
    }

    /// <summary>
    /// Extracts search results from DuckDuckGo HTML response.
    /// </summary>
    private static List<(string Title, string Url, string Snippet)> ExtractSearchResults(string html)
    {
        var results = new List<(string, string, string)>();

        // Match result links from DuckDuckGo HTML
        var linkMatches = ResultLinkRegex().Matches(html);

        foreach (Match match in linkMatches)
        {
            if (match.Groups.Count >= 3)
            {
                var url = match.Groups[1].Value;
                var title = HttpUtility.HtmlDecode(match.Groups[2].Value.Trim());

                // Skip DuckDuckGo internal links
                if (url.StartsWith("https://duckduckgo.com", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                // Try to extract the actual URL from DuckDuckGo's redirect
                if (url.Contains("uddg=", StringComparison.OrdinalIgnoreCase))
                {
                    var uddgMatch = UddgRegex().Match(url);
                    if (uddgMatch.Success)
                    {
                        url = HttpUtility.UrlDecode(uddgMatch.Groups[1].Value);
                    }
                }

                results.Add((title, url, string.Empty));
            }
        }

        return results;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    [GeneratedRegex(@"<script[^>]*>[\s\S]*?</script>|<style[^>]*>[\s\S]*?</style>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptStyleRegex();

    [GeneratedRegex(@"<!--[\s\S]*?-->")]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex NewlineRegex();

    [GeneratedRegex(@"<a[^>]+class=""result__a""[^>]*href=""([^""]+)""[^>]*>([^<]+)</a>", RegexOptions.IgnoreCase)]
    private static partial Regex ResultLinkRegex();

    [GeneratedRegex(@"uddg=([^&]+)")]
    private static partial Regex UddgRegex();
}
