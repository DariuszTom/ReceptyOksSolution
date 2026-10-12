using HomeSeeker.Abstractions;
using HomeSeeker.Configuration;
using Microsoft.Extensions.Options;
using ReceptyOks.Api.Services;

namespace ReceptyOks.Api.Middleware;

/// <summary>
/// Background service that runs periodic scans and handles on-demand scan requests.
/// </summary>
public sealed class HomeSeekerScanService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ScanTriggerQueue _triggerQueue;
    private readonly HomeSeekerOptions _options;
    private readonly ILogger<HomeSeekerScanService> _logger;

    public HomeSeekerScanService(
        IServiceScopeFactory scopeFactory,
        ScanTriggerQueue triggerQueue,
        IOptions<HomeSeekerOptions> options,
        ILogger<HomeSeekerScanService> logger)
    {
        _scopeFactory = scopeFactory;
        _triggerQueue = triggerQueue;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HomeSeeker scan service starting. Enabled={Enabled}, Interval={Interval}",
            _options.Enabled, _options.ScanInterval);

        // Wait for startup delay
        await Task.Delay(_options.StartupDelay, stoppingToken).ConfigureAwait(false);

        // If disabled, only handle on-demand requests
        if (!_options.Enabled)
        {
            _logger.LogInformation("HomeSeeker periodic scanning is disabled. Only processing on-demand requests.");
            await HandleOnDemandOnlyAsync(stoppingToken).ConfigureAwait(false);
            return;
        }

        // Run initial scan
        await ScanActiveProfilesAsync(stoppingToken).ConfigureAwait(false);

        // Main loop: wait for either periodic timer tick or on-demand request.
        // PeriodicTimer allows only ONE pending WaitForNextTickAsync; we must keep the
        // same task across loop iterations and only re-request a new wait after the
        // previous one has actually completed. Likewise, keep a single pending
        // WaitToReadAsync for the trigger channel.
        using var timer = new PeriodicTimer(_options.ScanInterval);

        Task<bool>? timerTask = null;
        Task<bool>? queueWaitTask = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                timerTask ??= timer.WaitForNextTickAsync(stoppingToken).AsTask();
                queueWaitTask ??= _triggerQueue.Reader.WaitToReadAsync(stoppingToken).AsTask();

                var completed = await Task.WhenAny(timerTask, queueWaitTask).ConfigureAwait(false);

                if (stoppingToken.IsCancellationRequested)
                    break;

                if (ReferenceEquals(completed, queueWaitTask))
                {
                    // Observe the result; discard whether more items are expected.
                    await queueWaitTask.ConfigureAwait(false);
                    queueWaitTask = null;

                    // Drain all queued on-demand requests before looping.
                    while (_triggerQueue.Reader.TryRead(out var profileId))
                    {
                        await RunSingleScanAsync(profileId, onDemand: true, stoppingToken).ConfigureAwait(false);
                    }
                }
                else if (ReferenceEquals(completed, timerTask))
                {
                    var ticked = await timerTask.ConfigureAwait(false);
                    timerTask = null;

                    if (ticked)
                    {
                        await ScanActiveProfilesAsync(stoppingToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("HomeSeeker scan service is stopping");
                break;
            }
            catch (Exception ex)
            {
                // Never let the background service fault - that would stop the entire host
                // under the default BackgroundServiceExceptionBehavior.StopHost.
                _logger.LogError(ex, "Unhandled error in HomeSeeker scan loop; continuing");

                // Reset pending waits so we don't observe the same faulted task again.
                timerTask = null;
                queueWaitTask = null;

                // Small backoff to avoid a hot loop if the failure is persistent.
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task HandleOnDemandOnlyAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var profileId in _triggerQueue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                await RunSingleScanAsync(profileId, onDemand: true, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("HomeSeeker scan service is stopping");
        }
    }

    private async Task ScanActiveProfilesAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IListingRepository>();

            var profiles = await repository.GetActiveProfilesAsync(stoppingToken).ConfigureAwait(false);

            _logger.LogInformation("Found {Count} active profiles due for scanning", profiles.Count);

            foreach (var profile in profiles)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                await RunSingleScanAsync(profile.Id, onDemand: false, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during periodic scan");
        }
    }

    private async Task RunSingleScanAsync(Guid profileId, bool onDemand, CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IListingRepository>();

        bool acquired;
        try
        {
            // On-demand scans only need the lease; periodic scans also need the due check.
            acquired = onDemand
                ? await repository.TryAcquireScanLeaseAsync(profileId, stoppingToken).ConfigureAwait(false)
                : await repository.TryMarkProfileScannedAsync(profileId, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire scan lease for profile {ProfileId}", profileId);
            return;
        }

        if (!acquired)
        {
            _logger.LogDebug(
                "Scan lease for profile {ProfileId} not acquired (another instance is scanning or not yet due)",
                profileId);
            return;
        }

        var succeeded = false;
        try
        {
            var scanService = scope.ServiceProvider.GetRequiredService<IMarketScanService>();

            _logger.LogInformation("Starting scan for profile {ProfileId} (onDemand={OnDemand})",
                profileId, onDemand);

            succeeded = await scanService.RunScanAsync(profileId, stoppingToken).ConfigureAwait(false);

            _logger.LogInformation("Completed scan for profile {ProfileId} (succeeded={Succeeded})",
                profileId, succeeded);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning profile {ProfileId}", profileId);
        }
        finally
        {
            // Always release the lease so other instances can pick the profile up, but only
            // stamp LastScannedAt on success so failed/cancelled scans are retried next cycle.
            // Use CancellationToken.None so a shutdown cannot leave the row leased forever.
            try
            {
                await repository.ReleaseScanLeaseAsync(profileId, markScanned: succeeded, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to release scan lease for profile {ProfileId}", profileId);
            }
        }
    }
}
