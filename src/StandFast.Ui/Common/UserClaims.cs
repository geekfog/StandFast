using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace StandFast.Ui.Common;

/// <summary>
/// The one place identity is read out of a principal, used by both the request-scoped current user and the components that hold a circuit's
/// authentication state. Inbound claim mapping is switched off on the OpenID Connect handler, so claims arrive under their standard OIDC names
/// rather than the legacy SOAP URIs.
/// </summary>
public static class UserClaims
{
    /// <summary>The provider's stable subject identifier: what audit records are attributed to and what per-user settings are keyed by.</summary>
    public static string? UserId(this ClaimsPrincipal? principal) => principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);

    public static string? DisplayName(this ClaimsPrincipal? principal) => principal?.FindFirstValue(JwtRegisteredClaimNames.Name) ?? principal?.Identity?.Name;

    public static string? Email(this ClaimsPrincipal? principal) => principal?.FindFirstValue(JwtRegisteredClaimNames.Email);
}
