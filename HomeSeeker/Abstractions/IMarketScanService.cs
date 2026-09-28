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
    Task RunScanAsync(Guid profileId, CancellationToken cancellationToken = default);
}
