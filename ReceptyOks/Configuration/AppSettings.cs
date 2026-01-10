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
    /// Service name for Aspire service discovery
    /// </summary>
    public string? ApiServiceName { get; set; }

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