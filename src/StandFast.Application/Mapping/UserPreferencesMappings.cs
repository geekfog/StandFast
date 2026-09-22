using StandFast.Application.Dtos;
using StandFast.Domain.Entities;

namespace StandFast.Application.Mapping;

/// <summary>The only place <see cref="UserPreferences"/> becomes a DTO. Absent settings map to the defaults, so callers get an appearance to render either way.</summary>
public static class UserPreferencesMappings
{
    public static UserPreferencesDto ToDto(this UserPreferences? preferences) =>
        preferences is null ? UserPreferencesDto.Default : new UserPreferencesDto(preferences.IsDarkMode, preferences.Email);
}
