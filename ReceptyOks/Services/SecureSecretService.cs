using System.Security.Cryptography;
using System.Text;

namespace ReceptyOks.Services
{
    public static class SecureSecretService
    {
        public static async Task SaveAsync(string key, byte[] value)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (value is null) throw new ArgumentNullException(nameof(value));

            try
            {
                await SecureStorage.SetAsync(key, Convert.ToBase64String(value));
            }
            catch (FeatureNotSupportedException)
            {
                // Secure storage not available on this device/emulator
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                // Access denied (e.g., device policy, no lock screen)
                throw;
            }
        }
        // Read a secret from SecureStorage and return a mutable byte[].
        // Prefer storing secrets as base64 in SecureStorage; this returns the decoded bytes.
        public static async Task<byte[]> GetSecretBytesAsync(string key)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            var s = await SecureStorage.GetAsync(key).ConfigureAwait(false);
            if (string.IsNullOrEmpty(s)) return Array.Empty<byte>();

            // Try base64 (recommended for binary secrets); fallback to UTF8 bytes.
            try
            {
                return Convert.FromBase64String(s);
            }
            catch
            {
                return Encoding.UTF8.GetBytes(s);
            }
        }

        // Helper to run work with the secret and ensure buffer is zeroed afterwards.
        public static async Task UseSecretAsync(string key, Func<byte[], Task> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            var buffer = await GetSecretBytesAsync(key).ConfigureAwait(false);
            try
            {
                await action(buffer).ConfigureAwait(false);
            }
            finally
            {
                Clear(buffer);
            }
        }

        // Zero a buffer when done.
        public static void Clear(byte[]? buffer)
        {
            if (buffer is null || buffer.Length == 0) return;
            CryptographicOperations.ZeroMemory(buffer);
        }
    }
}
