namespace HomeSeeker.Abstractions;

/// <summary>
/// Service for orchestrating market scans.
/// </summary>
public interface IMarketScanService
{
    /// <summary>
    /// Runs a complete scan for a search profile.
    /// </summary>
    /// <param name="profileId">Profile ID to scan.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when the scan completed successfully; false when it was skipped, failed, or cancelled.</returns>
    Task<bool> RunScanAsync(Guid profileId, CancellationToken cancellationToken = default);
}
