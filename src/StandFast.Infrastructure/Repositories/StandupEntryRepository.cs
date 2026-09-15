using Azure.Data.Tables;
using StandFast.Domain.Abstractions;
using StandFast.Domain.Common;
using StandFast.Domain.Entities;
using StandFast.Infrastructure.Mapping;
using StandFast.Infrastructure.Storage;
using StandFast.Infrastructure.TableEntities;

namespace StandFast.Infrastructure.Repositories;

public sealed class StandupEntryRepository(ITableClientProvider tables) : IStandupEntryRepository
{
    public async Task<StandupEntryPair> GetCurrentAndPriorAsync(Guid standupId, Guid personId, DateOnly meetingDate, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.StandupEntries, cancellationToken);
        string partitionKey = StorageKeys.EntryPartitionKey(standupId, personId);
        string rowKey = StorageKeys.EntryRowKey(meetingDate);

        // Row keys are inverted dates, so a "greater than or equal" range walks backwards in time from the requested date.
        // The first two rows are therefore the requested date (when recorded) and the most recent earlier date.
        List<StandupEntry> found = [];
        string filter = TableClient.CreateQueryFilter($"PartitionKey eq {partitionKey} and RowKey ge {rowKey}");

        await foreach (StandupEntryTableEntity entity in client.QueryAsync<StandupEntryTableEntity>(filter, maxPerPage: DomainLimits.CurrentAndPriorFetchCount, cancellationToken: cancellationToken))
        {
            found.Add(entity.ToDomain());
            if (found.Count == DomainLimits.CurrentAndPriorFetchCount)
            {
                break;
            }
        }

        StandupEntry? current = found.FirstOrDefault(entry => entry.MeetingDate == meetingDate);
        StandupEntry? prior = found.FirstOrDefault(entry => entry.MeetingDate < meetingDate);

        return current is null && prior is null ? StandupEntryPair.Empty : new StandupEntryPair(current, prior);
    }

    public async Task UpsertAsync(StandupEntry entry, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.StandupEntries, cancellationToken);
        await client.UpsertEntityAsync(entry.ToTableEntity(), TableUpdateMode.Replace, cancellationToken);
    }
}
