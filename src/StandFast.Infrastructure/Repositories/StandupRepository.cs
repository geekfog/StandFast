using Azure;
using Azure.Data.Tables;
using StandFast.Domain.Abstractions;
using StandFast.Domain.Entities;
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

        // The roster is a child partition of the standup, so it is removed with the standup rather than left orphaned.
        TableClient memberClient = await tables.GetAsync(StorageNames.StandupMembers, cancellationToken);
        string partitionKey = StorageKeys.MemberPartitionKey(id);

        await foreach (StandupMemberTableEntity entity in memberClient.QueryAsync<StandupMemberTableEntity>(TableClient.CreateQueryFilter($"PartitionKey eq {partitionKey}"), cancellationToken: cancellationToken))
        {
            await memberClient.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, ETag.All, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<StandupMember>> GetMembersAsync(Guid standupId, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.StandupMembers, cancellationToken);
        string partitionKey = StorageKeys.MemberPartitionKey(standupId);
        List<StandupMember> members = [];

        await foreach (StandupMemberTableEntity entity in client.QueryAsync<StandupMemberTableEntity>(TableClient.CreateQueryFilter($"PartitionKey eq {partitionKey}"), cancellationToken: cancellationToken))
        {
            members.Add(entity.ToDomain());
        }

        return members;
    }

    /// <summary>
    /// The only query in the app without a partition key. Answering "which standups is this person on" means crossing every standup's partition,
    /// and the membership table holds one small row per person per standup, so a scan is cheaper here than a second index written on every change.
    /// </summary>
    public async Task<IReadOnlyList<StandupMember>> GetAllMembersAsync(CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.StandupMembers, cancellationToken);
        List<StandupMember> members = [];

        await foreach (StandupMemberTableEntity entity in client.QueryAsync<StandupMemberTableEntity>(cancellationToken: cancellationToken))
        {
            members.Add(entity.ToDomain());
        }

        return members;
    }

    public async Task UpsertMemberAsync(StandupMember member, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.StandupMembers, cancellationToken);
        await client.UpsertEntityAsync(member.ToTableEntity(), TableUpdateMode.Replace, cancellationToken);
    }

    public async Task RemoveMemberAsync(Guid standupId, Guid personId, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.StandupMembers, cancellationToken);
        await client.DeleteEntityAsync(StorageKeys.MemberPartitionKey(standupId), StorageKeys.MemberRowKey(personId), ETag.All, cancellationToken);
    }
}
