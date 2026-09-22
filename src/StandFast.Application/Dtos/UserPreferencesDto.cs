using StandFast.Domain.Common;
using StandFast.Domain.Entities;

namespace StandFast.Application.Dtos;

/// <summary>Read model for one user's settings, and the answer to "is this person me" wherever the UI marks the signed-in user's own row.</summary>
public sealed record UserPreferencesDto(bool IsDarkMode, string? Email)
{
    /// <summary>What a session runs with when nothing has been saved for the user, or when nobody is signed in.</summary>
    public static readonly UserPreferencesDto Default = new(UserPreferences.DefaultIsDarkMode, null);

    /// <summary>True when the address belongs to the signed-in user. Nobody matches when no address is known, so an anonymous session marks nothing.</summary>
    public bool IsSelf(string? email) => EmailAddress.Matches(Email, email);
}
