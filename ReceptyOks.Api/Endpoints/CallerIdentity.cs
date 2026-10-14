using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ReceptyOks.Api.Endpoints;

/// <summary>
/// Resolves the owner identity of the authenticated caller from the JWT principal.
/// </summary>
internal static class CallerIdentity
{
    /// <summary>
    /// Returns the caller's stable identifier (<c>sub</c> / NameIdentifier claim),
    /// or null when the principal is not authenticated or carries no usable subject.
    /// </summary>
    public static string? GetOwnerId(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        // The JWT handler may or may not map "sub" to NameIdentifier depending on
        // MapInboundClaims, so look at both.
        var ownerId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return string.IsNullOrWhiteSpace(ownerId) ? null : ownerId;
    }
}
