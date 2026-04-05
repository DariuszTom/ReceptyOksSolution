namespace ReceptyOks.Api.Services;

/// <summary>
/// Service interface for synchronization operations.
/// </summary>
public interface ISyncService
{
    /// <summary>
    /// Performs bidirectional synchronization between client and server.
    /// </summary>
    Task<SyncResponse> SyncAsync(SyncRequest request, DateTime? lastSyncedAt);

    /// <summary>
    /// Gets all data for initial synchronization.
    /// </summary>
    Task<SyncResponse> GetFullSyncAsync();

    /// <summary>
    /// Uploads all client data to server (overwrites server data).
    /// </summary>
    Task<SyncResponse> UploadAllAsync(SyncRequest request);
}
