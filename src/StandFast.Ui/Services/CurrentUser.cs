using System.Security.Claims;
using StandFast.Application.Abstractions;
using StandFast.Ui.Common;

namespace StandFast.Ui.Services;

/// <summary>
/// Reads the signed-in principal from the current HTTP context so the Application layer can attribute audit records without knowing about ASP.NET Core.
/// Which claim carries which piece of identity is defined once, in <see cref="UserClaims"/>.
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public string? UserId => Principal.UserId();

    public string? DisplayName => Principal.DisplayName();

    public string? Email => Principal.Email();
}
