using Azure;
using Azure.Data.Tables;
using StandFast.Domain.Abstractions;
using StandFast.Domain.Entities;
using StandFast.Infrastructure.Mapping;
using StandFast.Infrastructure.Storage;
using StandFast.Infrastructure.TableEntities;

namespace StandFast.Infrastructure.Repositories;

public sealed class UserPreferencesRepository(ITableClientProvider tables) : IUserPreferencesRepository
{
    public async Task<UserPreferences?> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.UserPreferences, cancellationToken);
        NullableResponse<UserPreferencesTableEntity> response = await client.GetEntityIfExistsAsync<UserPreferencesTableEntity>(
            StorageKeys.UserPreferencesPartition, StorageKeys.UserPreferencesRowKey(userId), cancellationToken: cancellationToken);

        return response.HasValue ? response.Value!.ToDomain() : null;
    }

    public async Task UpsertAsync(UserPreferences preferences, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.UserPreferences, cancellationToken);
        await client.UpsertEntityAsync(preferences.ToTableEntity(), TableUpdateMode.Replace, cancellationToken);
    }
}
