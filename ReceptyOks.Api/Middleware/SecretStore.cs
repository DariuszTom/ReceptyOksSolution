
using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ReceptyOks.Api.Middleware;

public sealed class SecretStore : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SecretStore> _logger;
    private readonly TimeSpan _ttl;

    private byte[]? _storedHashBytes;
    private byte[]? _hmacKeyBytes;
    private DateTime _lastRefreshUtc;
    private readonly object _sync = new();

    public SecretStore(IConfiguration configuration, ILogger<SecretStore> logger, TimeSpan? ttl = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ttl = ttl ?? TimeSpan.FromMinutes(5);
    }

    public void Initialize()
    {
        RefreshIfNeeded(force: true);
    }

    public (byte[]? PasswordHash, byte[]? SecretKey) GetSecrets()
    {
        RefreshIfNeeded();
        lock (_sync)
        {
            return (_storedHashBytes, _hmacKeyBytes);
        }
    }

    private void RefreshIfNeeded(bool force = false)
    {
        if (!force && (DateTime.UtcNow - _lastRefreshUtc) < _ttl)
            return;

        lock (_sync)
        {
            if (!force && (DateTime.UtcNow - _lastRefreshUtc) < _ttl)
                return;

            _logger.LogDebug("Refreshing secrets from configuration");

            // support both names: ApiAuth:PasswordHash or PasswordHash (existing vault)
            var passwordHash =  _configuration["PasswordHash"];
            var secretKey =  _configuration["SecretKey"];

            if (string.IsNullOrWhiteSpace(passwordHash))
            {
                _logger.LogError("KeyVault is configured but password hash is missing (PasswordHash)");
                throw new InvalidOperationException("Missing PasswordHash");
            }

            var newStored = DecodeStringToBytes(passwordHash.Trim());
            byte[]? newKey = null;
            if (!string.IsNullOrWhiteSpace(secretKey))
            {
                newKey = DecodeStringToBytes(secretKey.Trim());
            }

            // zero previous
            if (_storedHashBytes is not null)
            {
                CryptographicOperations.ZeroMemory(_storedHashBytes);
            }
            if (_hmacKeyBytes is not null)
            {
                CryptographicOperations.ZeroMemory(_hmacKeyBytes);
            }

            _storedHashBytes = newStored;
            _hmacKeyBytes = newKey;
            _lastRefreshUtc = DateTime.UtcNow;
        }
    }

    private static byte[] DecodeStringToBytes(string value)
    {
        // Try Base64
        try
        {
            var b = Convert.FromBase64String(value);
            if (b.Length > 0) return b;
        }
        catch { }

        // Try hex
        try
        {
            var hex = value;
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                hex = hex[2..];
            }
            var bytes = Convert.FromHexString(hex);
            if (bytes.Length > 0) return bytes;
        }
        catch { }

        // Fallback to UTF8
        return Encoding.UTF8.GetBytes(value);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_storedHashBytes is not null)
            {
                CryptographicOperations.ZeroMemory(_storedHashBytes);
                _storedHashBytes = null;
            }
            if (_hmacKeyBytes is not null)
            {
                CryptographicOperations.ZeroMemory(_hmacKeyBytes);
                _hmacKeyBytes = null;
            }
        }
    }
}
