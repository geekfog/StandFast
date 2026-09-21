using Azure.Data.Tables;
using StandFast.Domain.Abstractions;
using StandFast.Domain.Common;
using StandFast.Domain.Entities;
using StandFast.Domain.Enums;
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

    public async Task<IReadOnlyCollection<DateOnly>> GetPresentedDatesAsync(Guid standupId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.StandupEntries, cancellationToken);
        (string partitionFrom, string partitionTo) = StorageKeys.EntryPartitionRange(standupId);
        (string rowFrom, string rowTo) = StorageKeys.EntryRowKeyRange(from, to);
        int presented = (int)AttendanceState.Presented;

        // Both keys are bounded, so this reads one standup's slice of one date range instead of scanning the table. Only the date column is
        // projected, because the answer is a set of dates and a participant's update text can be large.
        string filter = TableClient.CreateQueryFilter(
            $"PartitionKey ge {partitionFrom} and PartitionKey le {partitionTo} and RowKey ge {rowFrom} and RowKey le {rowTo} and State eq {presented}");

        HashSet<DateOnly> dates = [];
        await foreach (StandupEntryTableEntity entity in client.QueryAsync<StandupEntryTableEntity>(filter, select: [nameof(StandupEntryTableEntity.MeetingDateKey)], cancellationToken: cancellationToken))
        {
            dates.Add(MeetingCalendar.FromDateKey(entity.MeetingDateKey));
        }

        return dates;
    }
}
