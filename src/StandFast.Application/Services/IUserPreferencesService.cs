using StandFast.Application.Dtos;

namespace StandFast.Application.Services;

/// <summary>
/// Settings belonging to a signed-in user. The user is passed in rather than read from <see cref="Abstractions.ICurrentUser"/>, because the screen that
/// reads and writes these runs inside an interactive Blazor circuit, where there is no HTTP context and the principal comes from the circuit's own
/// authentication state.
/// </summary>
public interface IUserPreferencesService
{
    /// <summary>
    /// Settings for the given user, recording the address the provider reports when it is new or has changed since last time. Falls back to
    /// <see cref="UserPreferencesDto.Default"/> when nobody is signed in.
    /// </summary>
    Task<UserPreferencesDto> LoadAsync(string? userId, string? email, CancellationToken cancellationToken = default);

    Task SetDarkModeAsync(string? userId, bool isDarkMode, CancellationToken cancellationToken = default);
}
