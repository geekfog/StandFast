using StandFast.Domain.Entities;

namespace StandFast.Domain.Abstractions;

public interface IUserPreferencesRepository
{
    /// <summary>Settings for one user, or null when that user has never saved any.</summary>
    Task<UserPreferences?> GetAsync(string userId, CancellationToken cancellationToken = default);

    Task UpsertAsync(UserPreferences preferences, CancellationToken cancellationToken = default);
}
