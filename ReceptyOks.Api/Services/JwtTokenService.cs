using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace ReceptyOks.Api.Services;

/// <summary>
/// Issues HMAC-SHA256 signed JWTs using the same <c>Jwt:Key</c> that the JwtBearer
/// handler validates against. The token's <c>sub</c> claim carries the caller's
/// stable client/owner id and is what <see cref="Endpoints.CallerIdentity"/> reads
/// to scope HomeSeeker data.
/// </summary>
public sealed class JwtTokenService
{
    // Short-lived: the client (AccessTokenRefresher) re-issues it on demand via /api/auth/validate.
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(1);

    public const string KeyConfigKey = "Jwt:Key";
    public const string IssuerConfigKey = "Jwt:Issuer";
    public const string AudienceConfigKey = "Jwt:Audience";

    private readonly SigningCredentials _credentials;
    private readonly JsonWebTokenHandler _handler = new();
    private readonly string _issuer;
    private readonly string _audience;

    public JwtTokenService(IConfiguration configuration)
    {
        var key = GetRequired(configuration, KeyConfigKey);
        _issuer = GetRequired(configuration, IssuerConfigKey);
        _audience = GetRequired(configuration, AudienceConfigKey);
        _credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);
    }

    public (string Token, DateTime ExpiresAtUtc) Issue(string ownerId, TimeSpan? lifetime = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        var now = DateTime.UtcNow;
        var expires = now.Add(lifetime ?? DefaultLifetime);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, ownerId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            ]),
            Issuer = _issuer,
            Audience = _audience,
            NotBefore = now,
            IssuedAt = now,
            Expires = expires,
            SigningCredentials = _credentials
        };

        return (_handler.CreateToken(descriptor), expires);
    }

    public static string GetRequired(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{key} is not configured.")
            : value;
    }
}
