namespace ReceptyOks.Configuration;

/// <summary>
/// Application-wide configuration settings loaded from appsettings.json
/// </summary>
public class AppSettings
{
    public DatabaseSettings Database { get; set; } = new();
    public HttpSettings Http { get; set; } = new();
}

/// <summary>
/// Database configuration settings
/// </summary>
public class DatabaseSettings
{
    /// <summary>
    /// Name of the local SQLite database file
    /// </summary>
    public string LocalDatabaseName { get; set; }=string.Empty;

    /// <summary>
    /// Full path to the local database file in app data directory
    /// </summary>
    public string LocalDatabasePath =>
        Path.Combine(FileSystem.AppDataDirectory, LocalDatabaseName);
}

/// <summary>
/// HTTP client configuration settings for API communication
/// </summary>
public class HttpSettings
{
    /// <summary>
    /// Service name for Aspire service discovery (used in development with AppHost)
    /// </summary>
    public string? ApiServiceName { get; set; }

    /// <summary>
    /// Production API base URL (used when not running with Aspire)
    /// </summary>
    public string? ApiBaseUrl { get; set; }

    /// <summary>
    /// Default timeout in seconds for HTTP requests
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of retries for HTTP requests
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// GitHub API settings for update checking
    /// </summary>
    public GitHubSettings Github { get; set; } = new();

    /// <summary>
    /// Gets the effective API URL based on environment (development with Aspire or production)
    /// </summary>
    public string GetEffectiveApiUrl()
    {
#if DEBUG
        // In debug mode with Aspire, use service discovery
        if (!string.IsNullOrWhiteSpace(ApiServiceName))
        {
            return $"http://{ApiServiceName}";
        }
#endif
        // In release or when ApiServiceName is not set, use production URL
        if (string.IsNullOrWhiteSpace(ApiBaseUrl))
        {
            throw new InvalidOperationException("ApiBaseUrl must be configured for production builds");
        }
        return ApiBaseUrl;
    }
}

/// <summary>
/// GitHub API configuration for update checking
/// </summary>
public class GitHubSettings
{
    /// <summary>
    /// Base URL for GitHub releases API
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
    public string ReleaseEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// User-Agent header value for GitHub API requests
    /// </summary>
    public string UserAgent { get; set; } =string.Empty;
}
