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

        // Main loop: wait for either periodic timer tick or on-demand request
        using var timer = new PeriodicTimer(_options.ScanInterval);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Wait for either timer tick or queue item
                var task = await Task.WhenAny(
                    timer.WaitForNextTickAsync(stoppingToken).AsTask(),
                    _triggerQueue.Reader.WaitToReadAsync(stoppingToken).AsTask()
                ).ConfigureAwait(false);

                if (stoppingToken.IsCancellationRequested)
                    break;

                // Process any queued on-demand requests
                while (_triggerQueue.Reader.TryRead(out var profileId))
                {
                    await RunSingleScanAsync(profileId, stoppingToken).ConfigureAwait(false);
                }

                // If timer ticked, run periodic scan of all active profiles
                if (task is Task<bool> timerTask && await timerTask.ConfigureAwait(false))
                {
                    await ScanActiveProfilesAsync(stoppingToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("HomeSeeker scan service is stopping");
        }
    }

    private async Task HandleOnDemandOnlyAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var profileId in _triggerQueue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                await RunSingleScanAsync(profileId, stoppingToken).ConfigureAwait(false);
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

                await RunSingleScanAsync(profile.Id, stoppingToken).ConfigureAwait(false);
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

    private async Task RunSingleScanAsync(Guid profileId, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IListingRepository>();

            // Try to mark profile as scanned (distributed locking)
            var acquired = await repository.TryMarkProfileScannedAsync(profileId, stoppingToken)
                .ConfigureAwait(false);

            if (!acquired)
            {
                _logger.LogDebug("Profile {ProfileId} is already being scanned by another instance", profileId);
                return;
            }

            var scanService = scope.ServiceProvider.GetRequiredService<IMarketScanService>();

            _logger.LogInformation("Starting scan for profile {ProfileId}", profileId);

            await scanService.RunScanAsync(profileId, stoppingToken).ConfigureAwait(false);

            _logger.LogInformation("Completed scan for profile {ProfileId}", profileId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning profile {ProfileId}", profileId);
        }
    }
}
