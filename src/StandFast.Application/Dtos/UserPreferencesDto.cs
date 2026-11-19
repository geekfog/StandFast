using StandFast.Domain.Common;
using StandFast.Domain.Entities;

namespace StandFast.Application.Dtos;

/// <summary>Read model for one user's settings, and the answer to "is this person me" wherever the UI marks the signed-in user's own row.</summary>
/// <param name="PersonId">The person in People with the user's address, the one the board marks with a star. Null when nobody in People has it.</param>
public sealed record UserPreferencesDto(bool IsDarkMode, bool IsNavigationExpanded, string? Email, Guid? PersonId)
{
    /// <summary>What a session runs with when nothing has been saved for the user, or when nobody is signed in.</summary>
    public static readonly UserPreferencesDto Default = new(UserPreferences.DefaultIsDarkMode, UserPreferences.DefaultIsNavigationExpanded, null, null);

    /// <summary>True when the user is someone in People, which is who has a task list.</summary>
    public bool IsInPeople => PersonId is not null;

    /// <summary>True when the address belongs to the signed-in user. Nobody matches when no address is known, so an anonymous session marks nothing.</summary>
    public bool IsSelf(string? email) => EmailAddress.Matches(Email, email);
}
