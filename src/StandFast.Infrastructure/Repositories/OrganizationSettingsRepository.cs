using Azure;
using Azure.Data.Tables;
using StandFast.Domain.Abstractions;
using StandFast.Domain.Entities;
using StandFast.Infrastructure.Mapping;
using StandFast.Infrastructure.Storage;
using StandFast.Infrastructure.TableEntities;

namespace StandFast.Infrastructure.Repositories;

public sealed class OrganizationSettingsRepository(ITableClientProvider tables) : IOrganizationSettingsRepository
{
    public async Task<OrganizationSettings?> GetAsync(CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.OrganizationSettings, cancellationToken);
        NullableResponse<OrganizationSettingsTableEntity> response = await client.GetEntityIfExistsAsync<OrganizationSettingsTableEntity>(
            StorageKeys.OrganizationSettingsPartition, StorageKeys.OrganizationSettingsRowKey, cancellationToken: cancellationToken);

        return response.HasValue ? response.Value!.ToDomain() : null;
    }

    public async Task UpsertAsync(OrganizationSettings settings, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.OrganizationSettings, cancellationToken);
        await client.UpsertEntityAsync(settings.ToTableEntity(), TableUpdateMode.Replace, cancellationToken);
    }
}
