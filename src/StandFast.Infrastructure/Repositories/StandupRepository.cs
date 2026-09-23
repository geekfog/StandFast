using Azure;
using Azure.Data.Tables;
using StandFast.Domain.Abstractions;
using StandFast.Domain.Entities;
using StandFast.Domain.Enums;
using StandFast.Infrastructure.Mapping;
using StandFast.Infrastructure.Storage;
using StandFast.Infrastructure.TableEntities;

namespace StandFast.Infrastructure.Repositories;

public sealed class StandupRepository(ITableClientProvider tables) : IStandupRepository
{
    public async Task<IReadOnlyList<Standup>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.Standups, cancellationToken);
        List<Standup> standups = [];

        await foreach (StandupTableEntity entity in client.QueryAsync<StandupTableEntity>(TableClient.CreateQueryFilter($"PartitionKey eq {StorageKeys.StandupPartition}"), cancellationToken: cancellationToken))
        {
            standups.Add(entity.ToDomain());
        }

        return standups;
    }

    public async Task<Standup?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.Standups, cancellationToken);
        NullableResponse<StandupTableEntity> response = await client.GetEntityIfExistsAsync<StandupTableEntity>(StorageKeys.StandupPartition, StorageKeys.StandupRowKey(id), cancellationToken: cancellationToken);
        return response.HasValue ? response.Value!.ToDomain() : null;
    }

    public async Task UpsertAsync(Standup standup, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.Standups, cancellationToken);
        await client.UpsertEntityAsync(standup.ToTableEntity(), TableUpdateMode.Replace, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        TableClient standupClient = await tables.GetAsync(StorageNames.Standups, cancellationToken);
        await standupClient.DeleteEntityAsync(StorageKeys.StandupPartition, StorageKeys.StandupRowKey(id), ETag.All, cancellationToken);

        // Every roster and the meeting record are child partitions of the standup, so they are removed with it rather than left orphaned.
        foreach (string childTable in RosterRoleExtensions.InDisplayOrder.Select(StorageNames.MemberTable).Append(StorageNames.StandupMeetings))
        {
            await ClearPartitionAsync(childTable, StorageKeys.StandupChildPartitionKey(id), cancellationToken);
        }
    }

    public async Task<IReadOnlyList<StandupMember>> GetMembersAsync(Guid standupId, RosterRole role, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.MemberTable(role), cancellationToken);
        string partitionKey = StorageKeys.StandupChildPartitionKey(standupId);
        List<StandupMember> members = [];

        await foreach (StandupMemberTableEntity entity in client.QueryAsync<StandupMemberTableEntity>(TableClient.CreateQueryFilter($"PartitionKey eq {partitionKey}"), cancellationToken: cancellationToken))
        {
            members.Add(entity.ToDomain(role));
        }

        return members;
    }

    /// <summary>
    /// The only query in the app without a partition key. Answering "which standups is this person on" means crossing every standup's partition,
    /// and the membership table holds one small row per person per standup, so a scan is cheaper here than a second index written on every change.
    /// </summary>
    public async Task<IReadOnlyList<StandupMember>> GetAllMembersAsync(RosterRole role, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.MemberTable(role), cancellationToken);
        List<StandupMember> members = [];

        await foreach (StandupMemberTableEntity entity in client.QueryAsync<StandupMemberTableEntity>(cancellationToken: cancellationToken))
        {
            members.Add(entity.ToDomain(role));
        }

        return members;
    }

    public async Task UpsertMemberAsync(StandupMember member, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.MemberTable(member.Role), cancellationToken);
        await client.UpsertEntityAsync(member.ToTableEntity(), TableUpdateMode.Replace, cancellationToken);
    }

    public async Task RemoveMemberAsync(Guid standupId, Guid personId, RosterRole role, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.MemberTable(role), cancellationToken);
        await client.DeleteEntityAsync(StorageKeys.StandupChildPartitionKey(standupId), StorageKeys.MemberRowKey(personId), ETag.All, cancellationToken);
    }

    public async Task<StandupMeeting?> GetMeetingAsync(Guid standupId, DateOnly meetingDate, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.StandupMeetings, cancellationToken);
        NullableResponse<StandupMeetingTableEntity> response = await client.GetEntityIfExistsAsync<StandupMeetingTableEntity>(
            StorageKeys.StandupChildPartitionKey(standupId), StorageKeys.MeetingRowKey(meetingDate), cancellationToken: cancellationToken);

        return response.HasValue ? response.Value!.ToDomain() : null;
    }

    public async Task UpsertMeetingAsync(StandupMeeting meeting, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.StandupMeetings, cancellationToken);
        await client.UpsertEntityAsync(meeting.ToTableEntity(), TableUpdateMode.Replace, cancellationToken);
    }

    public async Task<IReadOnlyList<StandupMeeting>> GetMeetingsAsync(Guid standupId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.StandupMeetings, cancellationToken);

        // Row keys are plain dates, so bounding them reads one standup's slice of the period rather than its whole partition.
        string filter = TableClient.CreateQueryFilter(
            $"PartitionKey eq {StorageKeys.StandupChildPartitionKey(standupId)} and RowKey ge {StorageKeys.MeetingRowKey(from)} and RowKey le {StorageKeys.MeetingRowKey(to)}");

        List<StandupMeeting> meetings = [];
        await foreach (StandupMeetingTableEntity entity in client.QueryAsync<StandupMeetingTableEntity>(filter, cancellationToken: cancellationToken))
        {
            meetings.Add(entity.ToDomain());
        }

        return meetings;
    }

    private async Task ClearPartitionAsync(string logicalTableName, string partitionKey, CancellationToken cancellationToken)
    {
        TableClient client = await tables.GetAsync(logicalTableName, cancellationToken);

        await foreach (TableEntity entity in client.QueryAsync<TableEntity>(TableClient.CreateQueryFilter($"PartitionKey eq {partitionKey}"), select: [nameof(ITableEntity.RowKey)], cancellationToken: cancellationToken))
        {
            await client.DeleteEntityAsync(partitionKey, entity.RowKey, ETag.All, cancellationToken);
        }
    }
}
