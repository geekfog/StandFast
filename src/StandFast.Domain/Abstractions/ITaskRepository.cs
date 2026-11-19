using StandFast.Domain.Entities;

namespace StandFast.Domain.Abstractions;

/// <summary>Task states, shared by the whole organization, and the tasks on each person's list.</summary>
public interface ITaskRepository
{
    Task<IReadOnlyList<TaskState>> GetStatesAsync(CancellationToken cancellationToken = default);

    Task<TaskState?> GetStateAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpsertStateAsync(TaskState state, CancellationToken cancellationToken = default);

    Task DeleteStateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkTask>> GetForOwnerAsync(Guid ownerPersonId, CancellationToken cancellationToken = default);

    Task<WorkTask?> GetAsync(Guid ownerPersonId, Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>True when any person's task is in the state, which keeps the state from being deleted.</summary>
    Task<bool> IsStateInUseAsync(Guid stateId, CancellationToken cancellationToken = default);

    Task UpsertAsync(WorkTask task, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid ownerPersonId, Guid taskId, CancellationToken cancellationToken = default);
}
