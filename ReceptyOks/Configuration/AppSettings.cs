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
    public string LocalDatabaseName { get; set; } = "recipes_local.db";
    
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
    public string ApiServiceName { get; set; } = "receptyoks-api";
    
    /// <summary>
    /// Default timeout in seconds for HTTP requests
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 30;
}
