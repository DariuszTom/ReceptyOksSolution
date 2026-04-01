using ReceptyOks.Shared.Models;
using System.Text.Json;

namespace ReceptyOks.Services;

internal class UserService
{
    private const string UserStorageKey = "user_data";
    private User? _cachedUser;

    public static readonly Lazy<UserService> Instance = new(() => new UserService(), LazyThreadSafetyMode.ExecutionAndPublication);

    private UserService() { }

    /// <summary>
    /// Retrieves the user from cache or SecureStorage.
    /// </summary>
    public async Task<User?> GetUserAsync()
    {
        if (_cachedUser is not null)
        {
            return _cachedUser;
        }

        var json = await SecureStorage.Default.GetAsync(UserStorageKey).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        _cachedUser = JsonSerializer.Deserialize<User>(json);
        return _cachedUser;
    }

    /// <summary>
    /// Stores the user in SecureStorage and updates the cache.
    /// </summary>
    public async Task SetUserAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var json = JsonSerializer.Serialize(user);
        await SecureStorage.Default.SetAsync(UserStorageKey, json).ConfigureAwait(false);
        _cachedUser = user;
    }

    /// <summary>
    /// Removes the user from SecureStorage and clears the cache.
    /// </summary>
    public void ClearUser()
    {
        SecureStorage.Default.Remove(UserStorageKey);
        _cachedUser = null;
    }

    /// <summary>
    /// Checks if a user is stored.
    /// </summary>
    public async Task<bool> HasUserAsync()
    {
        if (_cachedUser is not null)
        {
            return true;
        }

        var json = await SecureStorage.Default.GetAsync(UserStorageKey).ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(json);
    }
}
