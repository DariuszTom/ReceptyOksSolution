using ReceptyOks.Shared;

namespace ReceptyOks.Services;

/// <summary>
/// Re-runs <c>/api/auth/validate</c> with the stored API secret so a new JWT lands in
/// <see cref="AccessTokenStore"/>. Concurrent callers share a single refresh.
/// </summary>
public sealed class AccessTokenRefresher(BackendAuthService authService) : IAccessTokenRefresher
{
    private static readonly SemaphoreSlim _gate = new(1, 1);

    // Bumped after every successful refresh. A caller that observes a bump while waiting for the
    // gate reuses that token instead of hitting the rate-limited /api/auth/validate again. A 401
    // received after this point still forces a new refresh because the caller re-enters with the new value.
    private static int _successfulRefreshes;

    public async Task<bool> HasValidTokenAsync() =>
        !string.IsNullOrEmpty(await AccessTokenStore.GetValidTokenAsync().ConfigureAwait(false));

    public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var observed = Volatile.Read(ref _successfulRefreshes);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _successfulRefreshes) != observed && await HasValidTokenAsync().ConfigureAwait(false))
                return true;

            var secret = await SecureSecretService.GetSecretBytesAsync(GlobalConstants.ApiKeyHeaderName).ConfigureAwait(false);
            if (secret is null || secret.Length == 0)
                return false;

            var (outcome, _) = await authService.TryValidateAsync(secret, cancellationToken).ConfigureAwait(false);
            var refreshed = outcome == AuthOutcome.Valid && await HasValidTokenAsync().ConfigureAwait(false);
            if (refreshed)
                Interlocked.Increment(ref _successfulRefreshes);
            return refreshed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }
}
