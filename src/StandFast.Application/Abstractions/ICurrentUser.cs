namespace StandFast.Application.Abstractions;

/// <summary>The signed-in Entra ID user, surfaced to the Application layer without a dependency on ASP.NET Core. Audit records are attributed through this.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Entra ID object id (the <c>oid</c> claim) when available.</summary>
    string? UserId { get; }

    string? DisplayName { get; }

    string? Email { get; }
}
