using System.Security.Claims;
using Microsoft.Identity.Web;
using StandFast.Application.Abstractions;

namespace StandFast.Ui.Services;

/// <summary>Reads the signed-in Entra ID principal from the current HTTP context so the Application layer can attribute audit records without knowing about ASP.NET Core.</summary>
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public string? UserId => Principal?.GetObjectId();

    public string? DisplayName => Principal?.FindFirstValue(ClaimConstants.Name) ?? Principal?.Identity?.Name;

    public string? Email => Principal?.GetDisplayName() ?? Principal?.FindFirstValue(ClaimTypes.Upn) ?? Principal?.FindFirstValue(ClaimTypes.Email);
}
