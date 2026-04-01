using ReceptyOks.Services;

namespace ReceptyOks.Interfaces
{
    public interface ISyncService
    {
        Task<SyncResult> FullSyncAsync();
        Task<SyncResult> SyncAsync();
        Task<SyncResult> UploadAllAsync();
    }
}