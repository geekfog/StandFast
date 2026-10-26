using Azure;
using Azure.Data.Tables;
using StandFast.Domain.Abstractions;
using StandFast.Domain.Entities;
using StandFast.Infrastructure.Mapping;
using StandFast.Infrastructure.Storage;
using StandFast.Infrastructure.TableEntities;

namespace StandFast.Infrastructure.Repositories;

public sealed class HolidayRepository(ITableClientProvider tables) : IHolidayRepository
{
    public async Task<IReadOnlyList<Holiday>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.Holidays, cancellationToken);
        List<Holiday> holidays = [];

        await foreach (HolidayTableEntity entity in client.QueryAsync<HolidayTableEntity>(TableClient.CreateQueryFilter($"PartitionKey eq {StorageKeys.HolidayPartition}"), cancellationToken: cancellationToken))
        {
            holidays.Add(entity.ToDomain());
        }

        return holidays;
    }

    public async Task<Holiday?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.Holidays, cancellationToken);
        NullableResponse<HolidayTableEntity> response = await client.GetEntityIfExistsAsync<HolidayTableEntity>(StorageKeys.HolidayPartition, StorageKeys.HolidayRowKey(id), cancellationToken: cancellationToken);
        return response.HasValue ? response.Value!.ToDomain() : null;
    }

    public async Task UpsertAsync(Holiday holiday, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.Holidays, cancellationToken);
        await client.UpsertEntityAsync(holiday.ToTableEntity(), TableUpdateMode.Replace, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.Holidays, cancellationToken);
        await client.DeleteEntityAsync(StorageKeys.HolidayPartition, StorageKeys.HolidayRowKey(id), ETag.All, cancellationToken);
    }
}
