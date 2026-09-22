using StandFast.Application.Dtos;
using StandFast.Application.Mapping;
using StandFast.Domain.Abstractions;
using StandFast.Domain.Common;
using StandFast.Domain.Entities;

namespace StandFast.Application.Services;

public sealed class UserPreferencesService(IUserPreferencesRepository preferences, IClock clock) : IUserPreferencesService
{
    public async Task<UserPreferencesDto> LoadAsync(string? userId, string? email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return UserPreferencesDto.Default;
        }

        UserPreferences? stored = await preferences.GetAsync(userId, cancellationToken);
        string? reported = EmailAddress.NormaliseOrNull(email);

        // Sign-in is the only moment the address is on hand, so it is written through whenever the provider reports one that differs from the stored copy.
        if (reported is not null && !string.Equals(stored?.Email, reported, StringComparison.Ordinal))
        {
            stored = Apply(stored, userId, settings => settings.Email = reported);
            await preferences.UpsertAsync(stored, cancellationToken);
        }

        return stored.ToDto();
    }

    /// <summary>An anonymous session has nowhere to save to, so its choice applies for as long as the session lasts and is then forgotten.</summary>
    public async Task SetDarkModeAsync(string? userId, bool isDarkMode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        UserPreferences stored = Apply(await preferences.GetAsync(userId, cancellationToken), userId, settings => settings.IsDarkMode = isDarkMode);
        await preferences.UpsertAsync(stored, cancellationToken);
    }

    /// <summary>Changes one part of a user's settings, starting a record for a user who has none yet and leaving every other part as it was.</summary>
    private UserPreferences Apply(UserPreferences? stored, string userId, Action<UserPreferences> change)
    {
        UserPreferences settings = stored ?? new UserPreferences { UserId = userId, CreatedUtc = clock.UtcNow };

        change(settings);
        settings.ModifiedUtc = stored is null ? null : clock.UtcNow;

        return settings;
    }
}
