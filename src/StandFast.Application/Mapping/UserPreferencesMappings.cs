using StandFast.Application.Dtos;
using StandFast.Domain.Entities;

namespace StandFast.Application.Mapping;

/// <summary>The only place <see cref="UserPreferences"/> becomes a DTO. Absent settings map to the defaults, so callers get an appearance to render either way.</summary>
public static class UserPreferencesMappings
{
    /// <summary>A user with no settings has no recorded address, so no person can be matched to them either.</summary>
    public static UserPreferencesDto ToDto(this UserPreferences? preferences, Guid? personId) =>
        preferences is null ? UserPreferencesDto.Default : new UserPreferencesDto(preferences.IsDarkMode, preferences.IsNavigationExpanded, preferences.Email, personId);
}
