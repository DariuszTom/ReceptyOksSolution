using ReceptyOks.Models;

namespace ReceptyOks.Interfaces;

/// <summary>
/// Service for checking backend health status
/// </summary>
public interface IHealthStatusService
{
    /// <summary>
    /// Gets the full health status from the backend including all health checks
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health status response with detailed information</returns>
    Task<HealthStatusResponse> GetHealthStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Quick liveness check - just verifies if the backend is responding
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if backend is alive, false otherwise</returns>
    Task<bool> IsAliveAsync(CancellationToken cancellationToken = default);
}
