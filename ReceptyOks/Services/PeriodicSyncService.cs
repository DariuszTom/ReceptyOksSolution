using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReceptyOks.Interfaces;
using ReceptyOks.Shared.Configuration;

namespace ReceptyOks.Services
{
    public class PeriodicSyncService : BackgroundService
    {
        private readonly ILogger<PeriodicSyncService> _logger;
        private readonly PeriodicSyncOptions _options;
        private readonly ISyncService _service;

        public PeriodicSyncService(ISyncService service, PeriodicSyncOptions options, ILogger<PeriodicSyncService> logger)
        {
            _options = options;
            _logger = logger;
            _service = service;

        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Let the app finish loading before running non-critical sync.
            await Task.Delay(_options.StartupDelay, stoppingToken).ConfigureAwait(false);

            if (stoppingToken.IsCancellationRequested) return;

            using PeriodicTimer timer = new(_options.Interval);
            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    SyncResult? syncResult = null;

                    switch (_options.SyncType)
                    {
                        case SyncType.Force:
                            syncResult = await _service.FullSyncAsync().ConfigureAwait(false);
                            break;
                        case SyncType.Normal:
                            syncResult = await _service.SyncAsync().ConfigureAwait(false);
                            break;
                        default:
                            _logger.LogInformation("{SyncType}. Skipping sync.", _options.SyncType);
                            break;
                    }

                    if (_options.ShowNotifications && syncResult != null)
                    {
                        await ShowNotificationAsync(syncResult).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Periodic sync service stopped.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during periodic sync.");
            }
        }

        private async Task ShowNotificationAsync(SyncResult syncResult)
        {
            if(syncResult == null) return;
            // Przełącz na main thread dla operacji UI
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (syncResult.Success)
                {
                    await SnackBarHelper.ShowInfoSnackbarAsync($"Periodic sync completed: {syncResult.Message}").ConfigureAwait(false);
                }
                else
                {
                    await SnackBarHelper.ShowErrorSnackbarAsync($"Periodic sync failed: {syncResult.Message}").ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }
    }
}

