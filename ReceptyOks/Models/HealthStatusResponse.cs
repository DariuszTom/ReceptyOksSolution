namespace ReceptyOks.Models;

/// <summary>
/// Response model for backend health status
/// </summary>
public class HealthStatusResponse
{
    /// <summary>
    /// Overall health status (Healthy, Degraded, Unhealthy)
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Total duration of all health checks
    /// </summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// Individual health check entries
    /// </summary>
    public Dictionary<string, HealthCheckEntry> Entries { get; set; } = [];

    /// <summary>
    /// Timestamp when health was checked
    /// </summary>
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Indicates if the health check request was successful
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Error message if the request failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Checks if overall status is Healthy
    /// </summary>
    public bool IsHealthy => Status.Equals("Healthy", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if overall status is Degraded
    /// </summary>
    public bool IsDegraded => Status.Equals("Degraded", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if overall status is Unhealthy
    /// </summary>
    public bool IsUnhealthy => Status.Equals("Unhealthy", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Individual health check entry
/// </summary>
public class HealthCheckEntry
{
    /// <summary>
    /// Status of this specific health check
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Duration of this health check
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Optional description or additional data
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Exception message if the check failed
    /// </summary>
    public string? Exception { get; set; }

    /// <summary>
    /// Additional data from the health check
    /// </summary>
    public Dictionary<string, object>? Data { get; set; }

    /// <summary>
    /// Tags associated with this health check
    /// </summary>
    public IEnumerable<string> Tags { get; set; } = [];
}
