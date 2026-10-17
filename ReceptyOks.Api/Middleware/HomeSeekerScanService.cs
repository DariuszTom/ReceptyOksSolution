using HomeSeeker.Abstractions;
using HomeSeeker.Configuration;
using Microsoft.Extensions.Options;

namespace ReceptyOks.Api.Middleware;

/// <summary>
/// Background service that periodically scans the single shared household profile.
/// The schedule (<see cref="HomeSeekerOptions.Enabled"/>, <see cref="HomeSeekerOptions.ScanInterval"/>)
/// is fixed at deployment; users influence only the scan parameters via the shared profile.
/// </summary>
public sealed class HomeSeekerScanService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HomeSeekerOptions _options;
    private readonly ILogger<HomeSeekerScanService> _logger;
    private readonly TimeProvider _time;
    private DateTimeOffset _retryNotBefore = DateTimeOffset.MinValue;

    public HomeSeekerScanService(
        IServiceScopeFactory scopeFactory,
        IOptions<HomeSeekerOptions> options,
        ILogger<HomeSeekerScanService> logger,
        TimeProvider? timeProvider = null)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _time = timeProvider ?? TimeProvider.System;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "HomeSeeker scan service starting. Enabled={Enabled}, Interval={Interval}, DueCheckEvery={Poll}",
            _options.Enabled, _options.ScanInterval, PollInterval(_options.ScanInterval));

        if (!_options.Enabled)
        {
            _logger.LogInformation("HomeSeeker periodic scanning is disabled; the scan service will stay idle.");
            return;
        }

        try
        {
            await Task.Delay(_options.StartupDelay, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // Scan immediately on startup (the due/lease check inside the repository makes
        // this a no-op when the profile was scanned recently), then on every tick.
        await ScanSharedProfileAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(PollInterval(_options.ScanInterval));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                    break;

                await ScanSharedProfileAsync(stoppingToken).ConfigureAwait(false);
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

    /// <summary>Upper bound on how late a due scan may start.</summary>
    public static readonly TimeSpan MaxPollInterval = TimeSpan.FromMinutes(30);

    /// <summary>
    /// How often to check whether a scan is due. Ticking once per <see cref="HomeSeekerOptions.ScanInterval"/>
    /// doubled the effective cycle: <c>LastScannedAt</c> is stamped when a scan finishes, so at the next
    /// tick the profile was still a few minutes short of due and the scan slipped a whole interval.
    /// Polling often is cheap (the due/lease check is one conditional UPDATE) and keeps scans on schedule.
    /// </summary>
    public static TimeSpan PollInterval(TimeSpan scanInterval) =>
        scanInterval < MaxPollInterval ? scanInterval : MaxPollInterval;

    /// <summary>Upper bound on the pause after a failed scan.</summary>
    public static readonly TimeSpan MaxFailedScanRetryDelay = TimeSpan.FromHours(1);

    /// <summary>
    /// Pause after a failed scan, so a blocked portal (403) is not hammered on every due check.
    /// </summary>
    public static TimeSpan FailedScanRetryDelay(TimeSpan scanInterval) =>
        scanInterval < MaxFailedScanRetryDelay ? scanInterval : MaxFailedScanRetryDelay;

    private async Task ScanSharedProfileAsync(CancellationToken stoppingToken)
    {
        if (_time.GetUtcNow() < _retryNotBefore)
            return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IListingRepository>();

        Guid profileId;
        Guid? leaseToken;
        try
        {
            var profile = await repository.GetSharedProfileAsync(stoppingToken).ConfigureAwait(false);
            if (profile is null)
            {
                _logger.LogInformation("HomeSeeker shared profile is not configured yet; skipping scan");
                return;
            }

            if (!profile.IsActive)
            {
                _logger.LogInformation("HomeSeeker shared profile is inactive; skipping scan");
                return;
            }

            profileId = profile.Id;
            leaseToken = await repository.TryAcquireScanLeaseAsync(profileId, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire scan lease for the shared profile");
            return;
        }

        if (leaseToken is null)
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

            _logger.LogInformation("Starting scan for profile {ProfileId}", profileId);

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
            _retryNotBefore = succeeded
                ? DateTimeOffset.MinValue
                : _time.GetUtcNow() + FailedScanRetryDelay(_options.ScanInterval);

            // Always release the lease so other instances can pick the profile up, but only
            // stamp LastScannedAt on success so failed/cancelled scans are retried next cycle.
            // Use CancellationToken.None so a shutdown cannot leave the row leased forever.
            try
            {
                var released = await repository
                    .ReleaseScanLeaseAsync(profileId, leaseToken.Value, markScanned: succeeded, CancellationToken.None)
                    .ConfigureAwait(false);

                if (!released)
                {
                    _logger.LogWarning(
                        "Scan lease for profile {ProfileId} was taken over by another instance before release; " +
                        "the scan likely exceeded the lease duration",
                        profileId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to release scan lease for profile {ProfileId}", profileId);
            }
        }
    }
}
