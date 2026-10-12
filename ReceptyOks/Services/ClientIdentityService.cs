namespace ReceptyOks.Services
{
    /// <summary>
    /// Provides a stable, per-installation identifier that the API uses as the owner of
    /// user-scoped data (e.g. HomeSeeker search profiles). The id is generated once and kept
    /// in SecureStorage, so it survives app restarts but is distinct for every install.
    /// </summary>
    public static class ClientIdentityService
    {
        public const string StorageKey = "ClientId";

        private static readonly SemaphoreSlim _gate = new(1, 1);
        private static string? _cached;

        public static async Task<string> GetOrCreateAsync()
        {
            if (_cached is not null) return _cached;

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_cached is not null) return _cached;

                var existing = await SecureStorage.GetAsync(StorageKey).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(existing))
                {
                    _cached = existing;
                    return existing;
                }

                var created = Guid.NewGuid().ToString("N");
                await SecureStorage.SetAsync(StorageKey, created).ConfigureAwait(false);
                _cached = created;
                return created;
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
