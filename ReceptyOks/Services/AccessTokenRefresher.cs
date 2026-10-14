using ReceptyOks.Shared;

namespace ReceptyOks.Services;

/// <summary>
/// Re-runs <c>/api/auth/validate</c> with the stored API secret so a new JWT lands in
/// <see cref="AccessTokenStore"/>. Concurrent callers share a single refresh.
/// </summary>
public sealed class AccessTokenRefresher(BackendAuthService authService) : IAccessTokenRefresher
{
    private static readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<bool> HasValidTokenAsync() =>
        !string.IsNullOrEmpty(await AccessTokenStore.GetValidTokenAsync().ConfigureAwait(false));

    public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var secret = await SecureSecretService.GetSecretBytesAsync(GlobalConstants.ApiKeyHeaderName).ConfigureAwait(false);
            if (secret is null || secret.Length == 0)
                return false;

            var (outcome, _) = await authService.TryValidateAsync(secret, cancellationToken).ConfigureAwait(false);
            return outcome == AuthOutcome.Valid && await HasValidTokenAsync().ConfigureAwait(false);
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
