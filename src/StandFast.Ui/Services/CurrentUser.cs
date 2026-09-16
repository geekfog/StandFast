using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using StandFast.Application.Abstractions;

namespace StandFast.Ui.Services;

/// <summary>
/// Reads the signed-in principal from the current HTTP context so the Application layer can attribute audit records without knowing about ASP.NET Core.
/// Inbound claim mapping is switched off on the OpenID Connect handler, so the claims arrive under their standard OIDC names rather than the legacy SOAP URIs.
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    /// <summary>The provider's stable subject identifier. This is what audit records are attributed to.</summary>
    public string? UserId => Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);

    public string? DisplayName => Principal?.FindFirstValue(JwtRegisteredClaimNames.Name) ?? Principal?.Identity?.Name;

    public string? Email => Principal?.FindFirstValue(JwtRegisteredClaimNames.Email);
}
