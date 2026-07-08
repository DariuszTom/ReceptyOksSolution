namespace HomeSeeker.Configuration;

/// <summary>
/// Configuration options for the HomeSeeker real estate scanner.
/// </summary>
public sealed record HomeSeekerOptions
{
    /// <summary>
    /// Section name in configuration.
    /// </summary>
    public const string SectionName = "HomeSeeker";

    /// <summary>
    /// Whether HomeSeeker background scanning is enabled.
    /// </summary>
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// Interval between automatic scans.
    /// </summary>
    public TimeSpan ScanInterval { get; init; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Delay after startup before the first scan.
    /// </summary>
    public TimeSpan StartupDelay { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Maximum search pages to fetch per portal per scan.
    /// </summary>
    public int MaxSearchPagesPerPortal { get; init; } = 5;

    /// <summary>
    /// Delay between HTTP requests to avoid rate limiting.
    /// </summary>
    public TimeSpan RequestDelay { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Maximum number of candidates to evaluate with AI per scan (cost control).
    /// </summary>
    public int MaxCandidatesPerScan { get; init; } = 8;

    /// <summary>
    /// Number of top listings to include in the email report.
    /// </summary>
    public int TopListingsInReport { get; init; } = 5;

    /// <summary>
    /// AI model to use for evaluation (cheaper model for cost control).
    /// </summary>
    public string Model { get; init; } = "claude-sonnet-4-20250514";

    /// <summary>
    /// Whether to enable web discovery (AI-based scraping of additional portals).
    /// </summary>
    public bool EnableWebDiscovery { get; init; } = false;

    /// <summary>
    /// Maximum results from web discovery scraper.
    /// </summary>
    public int WebDiscoveryMaxResults { get; init; } = 10;

    /// <summary>
    /// SMTP configuration for sending email reports.
    /// </summary>
    public SmtpOptions Smtp { get; init; } = new();
}

/// <summary>
/// SMTP configuration for sending email reports.
/// </summary>
public sealed record SmtpOptions
{
    /// <summary>
    /// SMTP server host.
    /// </summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>
    /// SMTP server port.
    /// </summary>
    public int Port { get; init; } = 587;

    /// <summary>
    /// SMTP login/username.
    /// </summary>
    public string Login { get; init; } = string.Empty;

    /// <summary>
    /// SMTP password.
    /// </summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// From email address.
    /// </summary>
    public string FromAddress { get; init; } = string.Empty;
}
