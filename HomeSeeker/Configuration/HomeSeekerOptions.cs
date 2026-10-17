using System.ComponentModel.DataAnnotations;

namespace HomeSeeker.Configuration;

/// <summary>
/// Configuration options for the HomeSeeker real estate scanner.
/// Validated at startup via <c>ValidateDataAnnotations().ValidateOnStart()</c>.
/// </summary>
public sealed class HomeSeekerOptions : IValidatableObject
{
    /// <summary>
    /// Section name in configuration.
    /// </summary>
    public const string SectionName = "HomeSeeker";

    /// <summary>
    /// Whether HomeSeeker background scanning is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Interval between automatic scans. Must be positive; also drives the
    /// PeriodicTimer, which throws on non-positive values (CODE_REVIEW.md C2/H9).
    /// </summary>
    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Delay after startup before the first scan.
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Maximum search pages to fetch per portal and per selected property type per scan.
    /// A profile searching both houses and apartments can send up to twice as many requests.
    /// </summary>
    [Range(1, 50)]
    public int MaxSearchPagesPerPortal { get; set; } = 5;

    /// <summary>
    /// Delay between HTTP requests to avoid rate limiting.
    /// </summary>
    public TimeSpan RequestDelay { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Maximum number of candidates to evaluate with AI per scan (cost control).
    /// Shared round-robin between the selected property types.
    /// </summary>
    [Range(1, 100)]
    public int MaxCandidatesPerScan { get; set; } = 8;

    /// <summary>
    /// Maximum number of non-candidate listings per scan whose ad page is fetched only to
    /// resolve a missing distance from the centre (search results without coordinates, e.g. Otodom).
    /// 0 disables the backfill.
    /// </summary>
    [Range(0, 50)]
    public int MaxDistanceBackfillPerScan { get; set; } = 5;

    /// <summary>
    /// Listing coordinates farther than this from the profile's city centre are treated as
    /// portal geocoding errors and ignored (no map pin, no distance).
    /// </summary>
    [Range(1, 500)]
    public double MaxCoordinateDistanceFromCityKm { get; set; } = 60;

    /// <summary>
    /// Number of top listings to include in the email report.
    /// </summary>
    [Range(1, 50)]
    public int TopListingsInReport { get; set; } = 5;

    /// <summary>
    /// Maximum listings from one development (or copies of the same offer across portals)
    /// that are evaluated by AI and shown in the report. The rest stay in the database.
    /// </summary>
    [Range(1, 50)]
    public int MaxListingsPerDevelopment { get; set; } = 2;

    /// <summary>
    /// AI model to use for evaluation. Defaults to a cheaper model for cost
    /// control; override via configuration when more capability is needed.
    /// </summary>
    [Required]
    public string Model { get; set; } = "claude-sonnet-5-5";

    /// <summary>
    /// Listings not seen in any scan for this long are hidden (soft-deleted), but only when their
    /// profile was scanned within this window (so pausing scans never wipes data). Hidden listings
    /// are hard-deleted once unseen for <see cref="ScanHistoryRetention"/>.
    /// </summary>
    public TimeSpan ListingRetention { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Completed scan runs (including their HTML reports) older than this are deleted.
    /// </summary>
    public TimeSpan ScanHistoryRetention { get; set; } = TimeSpan.FromDays(90);

    /// <summary>
    /// Interval between retention cleanups.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Whether apartment candidates get a transaction-price (RCN) valuation from deweloperuch.pl
    /// for the AI prompt. Only candidates with known coordinates are queried.
    /// </summary>
    public bool UseRcnValuation { get; set; } = true;

    /// <summary>
    /// Minimum interval between valuation requests.
    /// </summary>
    public TimeSpan RcnValuationRequestInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How long valuations stay disabled after the service answers 401/403/429.
    /// </summary>
    public TimeSpan RcnValuationBlockedCooldown { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// SMTP configuration for sending email reports.
    /// </summary>
    public SmtpOptions Smtp { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ScanInterval <= TimeSpan.Zero)
        {
            yield return new ValidationResult(
                "ScanInterval must be positive.", [nameof(ScanInterval)]);
        }

        if (StartupDelay < TimeSpan.Zero)
        {
            yield return new ValidationResult(
                "StartupDelay must be non-negative.", [nameof(StartupDelay)]);
        }

        if (RequestDelay < TimeSpan.Zero)
        {
            yield return new ValidationResult(
                "RequestDelay must be non-negative.", [nameof(RequestDelay)]);
        }

        if (RcnValuationRequestInterval < TimeSpan.FromSeconds(1))
        {
            yield return new ValidationResult(
                "RcnValuationRequestInterval must be at least 1 second.", [nameof(RcnValuationRequestInterval)]);
        }

        if (RcnValuationBlockedCooldown < TimeSpan.Zero)
        {
            yield return new ValidationResult(
                "RcnValuationBlockedCooldown must be non-negative.", [nameof(RcnValuationBlockedCooldown)]);
        }

        if (ListingRetention <= TimeSpan.Zero)
        {
            yield return new ValidationResult(
                "ListingRetention must be positive.", [nameof(ListingRetention)]);
        }

        if (ScanHistoryRetention <= TimeSpan.Zero)
        {
            yield return new ValidationResult(
                "ScanHistoryRetention must be positive.", [nameof(ScanHistoryRetention)]);
        }

        if (CleanupInterval <= TimeSpan.Zero)
        {
            yield return new ValidationResult(
                "CleanupInterval must be positive.", [nameof(CleanupInterval)]);
        }

        // SMTP is only required when scanning is enabled AND a profile may be
        // configured to send mail. Validate shape only when enabled; the sender
        // itself returns false for missing fields.
        if (Enabled && !string.IsNullOrWhiteSpace(Smtp.Host))
        {
            if (Smtp.Port is <= 0 or > 65535)
            {
                yield return new ValidationResult(
                    "Smtp.Port must be between 1 and 65535.", [$"{nameof(Smtp)}.{nameof(SmtpOptions.Port)}"]);
            }

            if (string.IsNullOrWhiteSpace(Smtp.FromAddress))
            {
                yield return new ValidationResult(
                    "Smtp.FromAddress is required when Smtp.Host is set.",
                    [$"{nameof(Smtp)}.{nameof(SmtpOptions.FromAddress)}"]);
            }
        }
    }
}

/// <summary>
/// SMTP configuration for sending email reports.
/// </summary>
/// <remarks>
/// This is a class (not a record) so the auto-generated <see cref="ToString"/>
/// does not leak the password into logs (CODE_REVIEW.md M14). <see cref="ToString"/>
/// is overridden to mask the password explicitly.
/// </remarks>
public sealed class SmtpOptions
{
    /// <summary>SMTP server host.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>SMTP server port.</summary>
    public int Port { get; set; } = 587;

    /// <summary>SMTP login/username.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>SMTP password. Not emitted by <see cref="ToString"/>.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>From email address.</summary>
    public string FromAddress { get; set; } = string.Empty;

    public override string ToString()
    {
        var passwordState = string.IsNullOrEmpty(Password) ? "<empty>" : "<redacted>";
        return $"{nameof(SmtpOptions)} {{ Host = {Host}, Port = {Port}, Login = {Login}, Password = {passwordState}, FromAddress = {FromAddress} }}";
    }
}
