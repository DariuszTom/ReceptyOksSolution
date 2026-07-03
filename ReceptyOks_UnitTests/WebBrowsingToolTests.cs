using System.Net;
using System.Text;
using Microsoft.Extensions.AI;
using Moq;
using Moq.Protected;
using ReceptyOks.Shared.AI;

namespace ReceptyOks_UnitTests;

/// <summary>
/// Unit tests for the <see cref="WebBrowsingTool"/> class.
/// </summary>
public sealed class WebBrowsingToolTests
{
    #region Constructor Tests

    /// <summary>
    /// Tests that the constructor creates an instance when no HttpClient is provided.
    /// </summary>
    [Test]
    public void Constructor_NoHttpClient_CreatesInstance()
    {
        // Act
        using var tool = new WebBrowsingTool();

        // Assert
        Assert.That(tool, Is.Not.Null);
    }

    /// <summary>
    /// Tests that the constructor creates an instance with a provided HttpClient.
    /// </summary>
    [Test]
    public void Constructor_WithHttpClient_CreatesInstance()
    {
        // Arrange
        using var httpClient = new HttpClient();

        // Act
        using var tool = new WebBrowsingTool(httpClient);

        // Assert
        Assert.That(tool, Is.Not.Null);
    }

    /// <summary>
    /// Tests that the constructor respects custom maxContentLength parameter.
    /// </summary>
    [Test]
    public async Task Constructor_CustomMaxContentLength_TruncatesContent()
    {
        // Arrange
        const int maxLength = 100;
        var longContent = new string('A', 200);
        var mockHandler = CreateMockHandler(longContent, "text/plain");
        using var httpClient = new HttpClient(mockHandler.Object);
        using var tool = new WebBrowsingTool(httpClient, maxLength);

        // Act
        var result = await tool.FetchWebPageAsync("https://example.com");

        // Assert
        Assert.That(result, Does.Contain("[Content truncated due to length...]"));
    }

    #endregion

    #region RegisterTools Tests

    /// <summary>
    /// Tests that RegisterTools throws ArgumentNullException when agent is null.
    /// </summary>
    [Test]
    public void RegisterTools_NullAgent_ThrowsArgumentNullException()
    {
        // Arrange
        using var tool = new WebBrowsingTool();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => tool.RegisterTools(null!));
        Assert.That(exception!.ParamName, Is.EqualTo("agent"));
    }

    /// <summary>
    /// Tests that RegisterTools adds two tools to the agent.
    /// </summary>
    [Test]
    public void RegisterTools_ValidAgent_AddsTwoTools()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        using var tool = new WebBrowsingTool();

        // Act
        tool.RegisterTools(agent);

        // Assert
        Assert.That(agent.Tools, Has.Count.EqualTo(2));
    }

    /// <summary>
    /// Tests that RegisterTools adds tools with correct names.
    /// </summary>
    [Test]
    public void RegisterTools_ValidAgent_AddsToolsWithCorrectNames()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        using var tool = new WebBrowsingTool();

        // Act
        tool.RegisterTools(agent);

        // Assert
        var toolNames = agent.Tools.OfType<AIFunction>().Select(t => t.Name).ToList();
        Assert.That(toolNames, Does.Contain("fetch_web_page"));
        Assert.That(toolNames, Does.Contain("search_web"));
    }

    #endregion

    #region FetchWebPageAsync Tests

    /// <summary>
    /// Tests that FetchWebPageAsync returns error for empty URL.
    /// </summary>
    [Test]
    public async Task FetchWebPageAsync_EmptyUrl_ReturnsError()
    {
        // Arrange
        using var tool = new WebBrowsingTool();

        // Act
        var result = await tool.FetchWebPageAsync("");

        // Assert
        Assert.That(result, Is.EqualTo("Error: URL cannot be empty."));
    }

    /// <summary>
    /// Tests that FetchWebPageAsync returns error for null URL.
    /// </summary>
    [Test]
    public async Task FetchWebPageAsync_NullUrl_ReturnsError()
    {
        // Arrange
        using var tool = new WebBrowsingTool();

        // Act
        var result = await tool.FetchWebPageAsync(null!);

        // Assert
        Assert.That(result, Is.EqualTo("Error: URL cannot be empty."));
    }

    /// <summary>
    /// Tests that FetchWebPageAsync returns error for whitespace-only URL.
    /// </summary>
    [Test]
    public async Task FetchWebPageAsync_WhitespaceUrl_ReturnsError()
    {
        // Arrange
        using var tool = new WebBrowsingTool();

        // Act
        var result = await tool.FetchWebPageAsync("   ");

        // Assert
        Assert.That(result, Is.EqualTo("Error: URL cannot be empty."));
    }

    /// <summary>
    /// Tests that FetchWebPageAsync returns error for invalid URL format.
    /// </summary>
    [TestCase("not-a-url")]
    [TestCase("ftp://example.com")]
    [TestCase("file:///c:/test.txt")]
    [TestCase("javascript:alert('test')")]
    public async Task FetchWebPageAsync_InvalidUrl_ReturnsError(string invalidUrl)
    {
        // Arrange
        using var tool = new WebBrowsingTool();

        // Act
        var result = await tool.FetchWebPageAsync(invalidUrl);

        // Assert
        Assert.That(result, Is.EqualTo("Error: Invalid URL. Please provide a valid HTTP or HTTPS URL."));
    }

    /// <summary>
    /// Tests that FetchWebPageAsync accepts valid HTTP URLs.
    /// </summary>
    [Test]
    public async Task FetchWebPageAsync_ValidHttpUrl_FetchesContent()
    {
        // Arrange
        const string expectedContent = "Hello World";
        var mockHandler = CreateMockHandler(expectedContent, "text/plain");
        using var httpClient = new HttpClient(mockHandler.Object);
        using var tool = new WebBrowsingTool(httpClient);

        // Act
        var result = await tool.FetchWebPageAsync("http://example.com");

        // Assert
        Assert.That(result, Does.Contain("Content from http://example.com"));
        Assert.That(result, Does.Contain(expectedContent));
    }

    /// <summary>
    /// Tests that FetchWebPageAsync accepts valid HTTPS URLs.
    /// </summary>
    [Test]
    public async Task FetchWebPageAsync_ValidHttpsUrl_FetchesContent()
    {
        // Arrange
        const string expectedContent = "Secure Content";
        var mockHandler = CreateMockHandler(expectedContent, "text/plain");
        using var httpClient = new HttpClient(mockHandler.Object);
        using var tool = new WebBrowsingTool(httpClient);

        // Act
        var result = await tool.FetchWebPageAsync("https://example.com");

        // Assert
        Assert.That(result, Does.Contain("Content from https://example.com"));
        Assert.That(result, Does.Contain(expectedContent));
    }

    /// <summary>
    /// Tests that FetchWebPageAsync returns error for non-success status codes.
    /// </summary>
    [TestCase(HttpStatusCode.NotFound, "404")]
    [TestCase(HttpStatusCode.InternalServerError, "500")]
    [TestCase(HttpStatusCode.Forbidden, "403")]
    [TestCase(HttpStatusCode.Unauthorized, "401")]
    public async Task FetchWebPageAsync_NonSuccessStatusCode_ReturnsError(HttpStatusCode statusCode, string expectedCode)
    {
        // Arrange
        var mockHandler = CreateMockHandler("", "text/plain", statusCode);
        using var httpClient = new HttpClient(mockHandler.Object);
        using var tool = new WebBrowsingTool(httpClient);

        // Act
        var result = await tool.FetchWebPageAsync("https://example.com");

        // Assert
        Assert.That(result, Does.StartWith("Error: Failed to fetch page."));
        Assert.That(result, Does.Contain(expectedCode));
    }

    /// <summary>
    /// Tests that FetchWebPageAsync returns error for empty content.
    /// </summary>
    [Test]
    public async Task FetchWebPageAsync_EmptyContent_ReturnsError()
    {
        // Arrange
        var mockHandler = CreateMockHandler("", "text/html");
        using var httpClient = new HttpClient(mockHandler.Object);
        using var tool = new WebBrowsingTool(httpClient);

        // Act
        var result = await tool.FetchWebPageAsync("https://example.com");

        // Assert
        Assert.That(result, Is.EqualTo("Error: The page returned empty content."));
    }

    /// <summary>
    /// Tests that FetchWebPageAsync extracts text from HTML content.
    /// </summary>
    [Test]
    public async Task FetchWebPageAsync_HtmlContent_ExtractsText()
    {
        // Arrange
        const string html = "<html><head><title>Test</title></head><body><p>Hello World</p></body></html>";
        var mockHandler = CreateMockHandler(html, "text/html");
        using var httpClient = new HttpClient(mockHandler.Object);
        using var tool = new WebBrowsingTool(httpClient);

        // Act
        var result = await tool.FetchWebPageAsync("https://example.com");

        // Assert
        Assert.That(result, Does.Contain("Hello World"));
        Assert.That(result, Does.Not.Contain("<html>"));
        Assert.That(result, Does.Not.Contain("<p>"));
    }

    /// <summary>
    /// Tests that FetchWebPageAsync removes script tags from HTML.
    /// </summary>
    [Test]
    public async Task FetchWebPageAsync_HtmlWithScripts_RemovesScripts()
    {
        // Arrange
        const string html = "<html><body><script>alert('danger');</script><p>Safe content</p></body></html>";
        var mockHandler = CreateMockHandler(html, "text/html");
        using var httpClient = new HttpClient(mockHandler.Object);
        using var tool = new WebBrowsingTool(httpClient);

        // Act
        var result = await tool.FetchWebPageAsync("https://example.com");

        // Assert
        Assert.That(result, Does.Contain("Safe content"));
        Assert.That(result, Does.Not.Contain("alert"));
        Assert.That(result, Does.Not.Contain("<script>"));
    }

    /// <summary>
    /// Tests that FetchWebPageAsync removes style tags from HTML.
    /// </summary>
    [Test]
    public async Task FetchWebPageAsync_HtmlWithStyles_RemovesStyles()
    {
        // Arrange
        const string html = "<html><head><style>body { color: red; }</style></head><body><p>Styled content</p></body></html>";
        var mockHandler = CreateMockHandler(html, "text/html");
        using var httpClient = new HttpClient(mockHandler.Object);
        using var tool = new WebBrowsingTool(httpClient);

        // Act
        var result = await tool.FetchWebPageAsync("https://example.com");

        // Assert
        Assert.That(result, Does.Contain("Styled content"));
        Assert.That(result, Does.Not.Contain("color: red"));
        Assert.That(result, Does.Not.Contain("<style>"));
    }

    /// <summary>
    /// Tests that FetchWebPageAsync removes HTML comments.
    /// </summary>
    [Test]
    public async Task FetchWebPageAsync_HtmlWithComments_RemovesComments()
    {
        // Arrange
        const string html = "<html><body><!-- This is a comment --><p>Visible content</p></body></html>";
        var mockHandler = CreateMockHandler(html, "text/html");
        using var httpClient = new HttpClient(mockHandler.Object);
        using var tool = new WebBrowsingTool(httpClient);

        // Act
        var result = await tool.FetchWebPageAsync("https://example.com");

        // Assert
        Assert.That(result, Does.Contain("Visible content"));
        Assert.That(result, Does.Not.Contain("This is a comment"));
        Assert.That(result, Does.Not.Contain("<!--"));
    }

    /// <summary>
    /// Tests that FetchWebPageAsync decodes HTML entities.
    /// </summary>
    [Test]
    public async Task FetchWebPageAsync_HtmlWithEntities_DecodesEntities()
    {
        // Arrange
        const string html = "<html><body><p>Tom &amp; Jerry &lt;3</p></body></html>";
        var mockHandler = CreateMockHandler(html, "text/html");
        using var httpClient = new HttpClient(mockHandler.Object);
        using var tool = new WebBrowsingTool(httpClient);

        // Act
        var result = await tool.FetchWebPageAsync("https://example.com");

        // Assert
        Assert.That(result, Does.Contain("Tom & Jerry <3"));
    }

    /// <summary>
    /// Tests that FetchWebPageAsync returns plain text content as-is.
    /// </summary>
    [Test]
    public async Task FetchWebPageAsync_PlainTextContent_ReturnsAsIs()
    {
        // Arrange
        const string plainText = "This is plain text content.\nWith multiple lines.";
        var mockHandler = CreateMockHandler(plainText, "text/plain");
        using var httpClient = new HttpClient(mockHandler.Object);
        using var tool = new WebBrowsingTool(httpClient);

        // Act
        var result = await tool.FetchWebPageAsync("https://example.com/text.txt");

        // Assert
        Assert.That(result, Does.Contain(plainText));
    }

    /// <summary>
    /// Tests that FetchWebPageAsync handles network errors gracefully.
    /// </summary>
    [Test]
    public async Task FetchWebPageAsync_NetworkError_ReturnsError()
    {
        // Arrange
        var mockHandler = CreateMockHandlerWithException(new HttpRequestException("Connection refused"));
        using var httpClient = new HttpClient(mockHandler.Object);
        using var tool = new WebBrowsingTool(httpClient);

        // Act
        var result = await tool.FetchWebPageAsync("https://example.com");

        // Assert
        Assert.That(result, Does.StartWith("Error: Network error while fetching page:"));
        Assert.That(result, Does.Contain("Connection refused"));
    }

    /// <summary>
    /// Tests that FetchWebPageAsync handles cancellation.
    /// </summary>
    [Test]
    public async Task FetchWebPageAsync_Cancelled_ReturnsTimeoutError()
    {
        // Arrange
        var mockHandler = CreateMockHandlerWithException(new TaskCanceledException());
        using var httpClient = new HttpClient(mockHandler.Object);
        using var tool = new WebBrowsingTool(httpClient);

        // Act
        var result = await tool.FetchWebPageAsync("https://example.com");

        // Assert
        Assert.That(result, Is.EqualTo("Error: Request timed out while fetching the page."));
    }

    /// <summary>
    /// Tests that FetchWebPageAsync truncates content exceeding max length.
    /// </summary>
    [Test]
    public async Task FetchWebPageAsync_ContentExceedsMaxLength_TruncatesContent()
    {
        // Arrange
        var longContent = new string('X', 60000);
        var mockHandler = CreateMockHandler(longContent, "text/plain");
        using var httpClient = new HttpClient(mockHandler.Object);
        using var tool = new WebBrowsingTool(httpClient, 50000);

        // Act
        var result = await tool.FetchWebPageAsync("https://example.com");

        // Assert
        Assert.That(result, Does.Contain("[Content truncated due to length...]"));
    }

    #endregion

    #region SearchWebAsync Tests

    /// <summary>
    /// Tests that SearchWebAsync returns error for empty query.
    /// </summary>
    [Test]
    public async Task SearchWebAsync_EmptyQuery_ReturnsError()
    {
        // Arrange
        using var tool = new WebBrowsingTool();

        // Act
        var result = await tool.SearchWebAsync("");

        // Assert
        Assert.That(result, Is.EqualTo("Error: Search query cannot be empty."));
    }

    /// <summary>
    /// Tests that SearchWebAsync returns error for null query.
    /// </summary>
    [Test]
    public async Task SearchWebAsync_NullQuery_ReturnsError()
    {
        // Arrange
        using var tool = new WebBrowsingTool();

        // Act
        var result = await tool.SearchWebAsync(null!);

        // Assert
        Assert.That(result, Is.EqualTo("Error: Search query cannot be empty."));
    }

    /// <summary>
    /// Tests that SearchWebAsync returns error for whitespace-only query.
    /// </summary>
    [Test]
    public async Task SearchWebAsync_WhitespaceQuery_ReturnsError()
    {
        // Arrange
        using var tool = new WebBrowsingTool();

        // Act
        var result = await tool.SearchWebAsync("   ");

        // Assert
        Assert.That(result, Is.EqualTo("Error: Search query cannot be empty."));
    }

    /// <summary>
    /// Tests that SearchWebAsync returns no results message when no results found.
    /// </summary>
    [Test]
    public async Task SearchWebAsync_NoResults_ReturnsNoResultsMessage()
    {
        // Arrange
        const string emptyResultsHtml = "<html><body><div>No results</div></body></html>";
        var mockHandler = CreateMockHandler(emptyResultsHtml, "text/html");
        using var httpClient = new HttpClient(mockHandler.Object);
        using var tool = new WebBrowsingTool(httpClient);

        // Act
        var result = await tool.SearchWebAsync("xyzabc123nonexistent");

        // Assert
        Assert.That(result, Does.Contain("No search results found for: xyzabc123nonexistent"));
    }

    /// <summary>
    /// Tests that SearchWebAsync parses DuckDuckGo results correctly.
    /// </summary>
    [Test]
    public async Task SearchWebAsync_ValidResults_ParsesResults()
    {
        // Arrange
        const string searchResultsHtml = """
            <html>
            <body>
                <a class="result__a" href="https://example.com/page1">First Result</a>
                <a class="result__a" href="https://example.com/page2">Second Result</a>
            </body>
            </html>
            """;
        var mockHandler = CreateMockHandler(searchResultsHtml, "text/html");
        using var httpClient = new HttpClient(mockHandler.Object);
        using var tool = new WebBrowsingTool(httpClient);

        // Act
        var result = await tool.SearchWebAsync("test query");

        // Assert
        Assert.That(result, Does.Contain("Search results for: test query"));
        Assert.That(result, Does.Contain("First Result"));
        Assert.That(result, Does.Contain("Second Result"));
    }

    /// <summary>
    /// Tests that SearchWebAsync handles DuckDuckGo redirect URLs.
    /// </summary>
    [Test]
    public async Task SearchWebAsync_RedirectUrls_ExtractsActualUrl()
    {
        // Arrange
        const string searchResultsHtml = """
            <html>
            <body>
                <a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2Factual-page">Test Result</a>
            </body>
            </html>
            """;
        var mockHandler = CreateMockHandler(searchResultsHtml, "text/html");
        using var httpClient = new HttpClient(mockHandler.Object);
        using var tool = new WebBrowsingTool(httpClient);

        // Act
        var result = await tool.SearchWebAsync("test");

        // Assert
        Assert.That(result, Does.Contain("https://example.com/actual-page"));
    }

    /// <summary>
    /// Tests that SearchWebAsync skips DuckDuckGo internal links.
    /// </summary>
    [Test]
    public async Task SearchWebAsync_InternalLinks_SkipsInternalLinks()
    {
        // Arrange
        const string searchResultsHtml = """
            <html>
            <body>
                <a class="result__a" href="https://duckduckgo.com/some-internal-page">Internal Link</a>
                <a class="result__a" href="https://example.com/external">External Link</a>
            </body>
            </html>
            """;
        var mockHandler = CreateMockHandler(searchResultsHtml, "text/html");
        using var httpClient = new HttpClient(mockHandler.Object);
        using var tool = new WebBrowsingTool(httpClient);

        // Act
        var result = await tool.SearchWebAsync("test");

        // Assert
        Assert.That(result, Does.Contain("External Link"));
        Assert.That(result, Does.Not.Contain("Internal Link"));
    }

    /// <summary>
    /// Tests that SearchWebAsync returns error for non-success status codes.
    /// </summary>
    [Test]
    public async Task SearchWebAsync_NonSuccessStatusCode_ReturnsError()
    {
        // Arrange
        var mockHandler = CreateMockHandler("", "text/html", HttpStatusCode.ServiceUnavailable);
        using var httpClient = new HttpClient(mockHandler.Object);
        using var tool = new WebBrowsingTool(httpClient);

        // Act
        var result = await tool.SearchWebAsync("test query");

        // Assert
        Assert.That(result, Does.StartWith("Error: Search failed with status code: 503"));
    }

    /// <summary>
    /// Tests that SearchWebAsync handles network errors gracefully.
    /// </summary>
    [Test]
    public async Task SearchWebAsync_NetworkError_ReturnsError()
    {
        // Arrange
        var mockHandler = CreateMockHandlerWithException(new HttpRequestException("Network unreachable"));
        using var httpClient = new HttpClient(mockHandler.Object);
        using var tool = new WebBrowsingTool(httpClient);

        // Act
        var result = await tool.SearchWebAsync("test query");

        // Assert
        Assert.That(result, Does.StartWith("Error: Network error during search:"));
        Assert.That(result, Does.Contain("Network unreachable"));
    }

    /// <summary>
    /// Tests that SearchWebAsync handles timeout gracefully.
    /// </summary>
    [Test]
    public async Task SearchWebAsync_Timeout_ReturnsError()
    {
        // Arrange
        var mockHandler = CreateMockHandlerWithException(new TaskCanceledException());
        using var httpClient = new HttpClient(mockHandler.Object);
        using var tool = new WebBrowsingTool(httpClient);

        // Act
        var result = await tool.SearchWebAsync("test query");

        // Assert
        Assert.That(result, Is.EqualTo("Error: Search request timed out."));
    }

    /// <summary>
    /// Tests that SearchWebAsync limits results to 10.
    /// </summary>
    [Test]
    public async Task SearchWebAsync_ManyResults_LimitsToTen()
    {
        // Arrange
        var resultsBuilder = new StringBuilder("<html><body>");
        for (var i = 1; i <= 15; i++)
        {
            resultsBuilder.Append($"""<a class="result__a" href="https://example.com/page{i}">Result {i}</a>""");
        }
        resultsBuilder.Append("</body></html>");

        var mockHandler = CreateMockHandler(resultsBuilder.ToString(), "text/html");
        using var httpClient = new HttpClient(mockHandler.Object);
        using var tool = new WebBrowsingTool(httpClient);

        // Act
        var result = await tool.SearchWebAsync("test");

        // Assert
        Assert.That(result, Does.Contain("Result 10"));
        Assert.That(result, Does.Not.Contain("Result 11"));
    }

    /// <summary>
    /// Tests that SearchWebAsync decodes HTML entities in titles.
    /// </summary>
    [Test]
    public async Task SearchWebAsync_HtmlEntitiesInTitle_DecodesEntities()
    {
        // Arrange
        const string searchResultsHtml = """
            <html>
            <body>
                <a class="result__a" href="https://example.com">Tom &amp; Jerry&#39;s Adventure</a>
            </body>
            </html>
            """;
        var mockHandler = CreateMockHandler(searchResultsHtml, "text/html");
        using var httpClient = new HttpClient(mockHandler.Object);
        using var tool = new WebBrowsingTool(httpClient);

        // Act
        var result = await tool.SearchWebAsync("tom jerry");

        // Assert
        Assert.That(result, Does.Contain("Tom & Jerry's Adventure"));
    }

    #endregion

    #region Dispose Tests

    /// <summary>
    /// Tests that Dispose disposes owned HttpClient.
    /// </summary>
    [Test]
    public void Dispose_OwnedHttpClient_DisposesClient()
    {
        // Arrange
        var tool = new WebBrowsingTool();

        // Act & Assert - should not throw
        Assert.DoesNotThrow(() => tool.Dispose());
    }

    /// <summary>
    /// Tests that Dispose does not dispose injected HttpClient.
    /// </summary>
    [Test]
    public async Task Dispose_InjectedHttpClient_DoesNotDisposeClient()
    {
        // Arrange
        var mockHandler = CreateMockHandler("test", "text/plain");
        using var httpClient = new HttpClient(mockHandler.Object);
        var tool = new WebBrowsingTool(httpClient);

        // Act
        tool.Dispose();

        // Assert - HttpClient should still be usable
        var response = await httpClient.GetAsync("https://example.com");
        Assert.That(response.IsSuccessStatusCode, Is.True);
    }

    /// <summary>
    /// Tests that multiple Dispose calls do not throw.
    /// </summary>
    [Test]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        // Arrange
        var tool = new WebBrowsingTool();

        // Act & Assert
        Assert.DoesNotThrow(() =>
        {
            tool.Dispose();
            tool.Dispose();
        });
    }

    #endregion

    #region Helper Methods

    private static Mock<HttpMessageHandler> CreateMockHandler(
        string content,
        string contentType,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, contentType)
        };

        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        return mockHandler;
    }

    private static Mock<HttpMessageHandler> CreateMockHandlerWithException(Exception exception)
    {
        var mockHandler = new Mock<HttpMessageHandler>();

        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(exception);

        return mockHandler;
    }

    #endregion
}
