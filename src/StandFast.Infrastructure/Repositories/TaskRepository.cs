using Azure;
using Azure.Data.Tables;
using StandFast.Domain.Abstractions;
using StandFast.Domain.Entities;
using StandFast.Infrastructure.Mapping;
using StandFast.Infrastructure.Storage;
using StandFast.Infrastructure.TableEntities;

namespace StandFast.Infrastructure.Repositories;

public sealed class TaskRepository(ITableClientProvider tables) : ITaskRepository
{
    public async Task<IReadOnlyList<TaskState>> GetStatesAsync(CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.TaskStates, cancellationToken);
        List<TaskState> states = [];

        await foreach (TaskStateTableEntity entity in client.QueryAsync<TaskStateTableEntity>(TableClient.CreateQueryFilter($"PartitionKey eq {StorageKeys.TaskStatePartition}"), cancellationToken: cancellationToken))
        {
            states.Add(entity.ToDomain());
        }

        return states;
    }

    public async Task<TaskState?> GetStateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.TaskStates, cancellationToken);
        NullableResponse<TaskStateTableEntity> response =
            await client.GetEntityIfExistsAsync<TaskStateTableEntity>(StorageKeys.TaskStatePartition, StorageKeys.TaskStateRowKey(id), cancellationToken: cancellationToken);
        return response.HasValue ? response.Value!.ToDomain() : null;
    }

    public async Task UpsertStateAsync(TaskState state, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.TaskStates, cancellationToken);
        await client.UpsertEntityAsync(state.ToTableEntity(), TableUpdateMode.Replace, cancellationToken);
    }

    public async Task DeleteStateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.TaskStates, cancellationToken);
        await client.DeleteEntityAsync(StorageKeys.TaskStatePartition, StorageKeys.TaskStateRowKey(id), ETag.All, cancellationToken);
    }

    public Task<IReadOnlyList<WorkTask>> GetForOwnerAsync(Guid ownerPersonId, CancellationToken cancellationToken = default) =>
        QueryTasksAsync(TableClient.CreateQueryFilter($"PartitionKey eq {StorageKeys.PersonChildPartitionKey(ownerPersonId)}"), cancellationToken: cancellationToken);

    public async Task<WorkTask?> GetAsync(Guid ownerPersonId, Guid taskId, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.Tasks, cancellationToken);
        NullableResponse<WorkTaskTableEntity> response =
            await client.GetEntityIfExistsAsync<WorkTaskTableEntity>(StorageKeys.PersonChildPartitionKey(ownerPersonId), StorageKeys.TaskRowKey(taskId), cancellationToken: cancellationToken);
        return response.HasValue ? response.Value!.ToDomain() : null;
    }

    /// <summary>Crosses every owner's partition, stopping at the first task found in the state.</summary>
    public async Task<bool> IsStateInUseAsync(Guid stateId, CancellationToken cancellationToken = default) =>
        (await QueryTasksAsync(TableClient.CreateQueryFilter($"StateId eq {stateId}"), maxCount: 1, cancellationToken)).Count > 0;

    public async Task UpsertAsync(WorkTask task, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.Tasks, cancellationToken);
        await client.UpsertEntityAsync(task.ToTableEntity(), TableUpdateMode.Replace, cancellationToken);
    }

    public async Task DeleteAsync(Guid ownerPersonId, Guid taskId, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.Tasks, cancellationToken);
        await client.DeleteEntityAsync(StorageKeys.PersonChildPartitionKey(ownerPersonId), StorageKeys.TaskRowKey(taskId), ETag.All, cancellationToken);
    }

    private async Task<IReadOnlyList<WorkTask>> QueryTasksAsync(string filter, int? maxCount = null, CancellationToken cancellationToken = default)
    {
        TableClient client = await tables.GetAsync(StorageNames.Tasks, cancellationToken);
        List<WorkTask> tasks = [];

        await foreach (WorkTaskTableEntity entity in client.QueryAsync<WorkTaskTableEntity>(filter, maxPerPage: maxCount, cancellationToken: cancellationToken))
        {
            tasks.Add(entity.ToDomain());
            if (tasks.Count == maxCount)
            {
                break;
            }
        }

        return tasks;
    }
}
