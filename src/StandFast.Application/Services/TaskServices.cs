using FluentValidation;
using StandFast.Application.Auditing;
using StandFast.Application.Dtos;
using StandFast.Application.Mapping;
using StandFast.Application.Validation;
using StandFast.Domain.Abstractions;
using StandFast.Domain.Common;
using StandFast.Domain.Entities;

namespace StandFast.Application.Services;

public interface ITaskStateService
{
    /// <summary>Every state, by group in the order work moves through them, then by title.</summary>
    Task<IReadOnlyList<TaskStateDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Refused when another state already has the same title.</summary>
    Task<Guid> SaveAsync(TaskStateEditDto state, CancellationToken cancellationToken = default);

    /// <summary>Refused while any task is in the state.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// The signed-in user's own tasks. The user's address is passed in rather than read from <see cref="Abstractions.ICurrentUser"/>, because the board
/// runs inside an interactive Blazor circuit, where there is no HTTP context. It is matched to People the same way the board marks the user's own card.
/// </summary>
public interface ITaskService
{
    /// <summary>The list belonging to whoever in People has the address, or null when nobody does.</summary>
    Task<TaskListDto?> GetForOwnerAsync(string? ownerEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves to the list of whoever in People has the address, returning the ids saved. Adding creates one task per title line, each with the same
    /// other settings; editing saves the one task. Refused when nobody has the address, or when the state or requester is no longer defined.
    /// </summary>
    Task<IReadOnlyList<Guid>> SaveAsync(string? ownerEmail, WorkTaskEditDto task, CancellationToken cancellationToken = default);

    Task DeleteAsync(string? ownerEmail, Guid taskId, CancellationToken cancellationToken = default);
}

public sealed class TaskStateService(ITaskRepository tasks, IClock clock, IValidator<TaskStateEditDto> validator, IAuditLog audit) : ITaskStateService
{
    private const string AuditTargetType = nameof(TaskState);

    public async Task<IReadOnlyList<TaskStateDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        [.. (await tasks.GetStatesAsync(cancellationToken)).InListOrder().Select(state => state.ToDto())];

    public async Task<Guid> SaveAsync(TaskStateEditDto state, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(state, cancellationToken);

        IReadOnlyList<TaskState> stored = await tasks.GetStatesAsync(cancellationToken);
        if (stored.FirstOrDefault(other => other.Id != state.Id && string.Equals(other.Title, state.Title.Trim(), StringComparison.OrdinalIgnoreCase)) is { } taken)
        {
            throw ValidationMessages.Failure(nameof(TaskStateEditDto.Title), ValidationMessages.TaskStateTitleTaken(taken.Title));
        }

        TaskState? existing = stored.FirstOrDefault(other => other.Id == state.Id);
        TaskState entity = existing ?? new TaskState { CreatedUtc = clock.UtcNow };
        state.ApplyTo(entity);
        entity.ModifiedUtc = existing is null ? null : clock.UtcNow;

        await tasks.UpsertStateAsync(entity, cancellationToken);
        audit.Record(existing is null ? AuditEvents.TaskStateCreated : AuditEvents.TaskStateUpdated, AuditTargetType, entity.Id.ToString(), Describe(entity));

        return entity.Id;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await tasks.GetStateAsync(id, cancellationToken) is not { } existing)
        {
            return;
        }

        if (await tasks.IsStateInUseAsync(id, cancellationToken))
        {
            throw ValidationMessages.Failure(nameof(TaskStateEditDto.Title), ValidationMessages.TaskStateInUse(existing.Title));
        }

        await tasks.DeleteStateAsync(id, cancellationToken);
        audit.Record(AuditEvents.TaskStateDeleted, AuditTargetType, id.ToString(), Describe(existing));
    }

    private static string Describe(TaskState state) => $"{state.Title} ({state.Group})";
}

public sealed class TaskService(ITaskRepository tasks, IPersonRepository people, IClock clock, IValidator<WorkTaskEditDto> validator, IAuditLog audit) : ITaskService
{
    private const string AuditTargetType = nameof(WorkTask);

    public async Task<TaskListDto?> GetForOwnerAsync(string? ownerEmail, CancellationToken cancellationToken = default)
    {
        if (PersonMatch.ByEmail(await people.GetAllAsync(cancellationToken), ownerEmail) is not { } owner)
        {
            return null;
        }

        Dictionary<Guid, TaskState> statesById = (await tasks.GetStatesAsync(cancellationToken)).ToDictionary(state => state.Id);

        IEnumerable<WorkTaskDto> list = (await tasks.GetForOwnerAsync(owner.Id, cancellationToken)).Select(task => task.ToDto(statesById.GetValueOrDefault(task.StateId)));

        return new TaskListDto(owner.Id, [.. list.InListOrder()]);
    }

    public async Task<IReadOnlyList<Guid>> SaveAsync(string? ownerEmail, WorkTaskEditDto task, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(task, cancellationToken);

        IReadOnlyList<Person> everyone = await people.GetAllAsync(cancellationToken);
        Person owner = PersonMatch.ByEmail(everyone, ownerEmail) ?? throw ValidationMessages.Failure(nameof(WorkTaskEditDto.Title), ValidationMessages.TaskOwnerNotFound);

        if (await tasks.GetStateAsync(task.StateId!.Value, cancellationToken) is null)
        {
            throw ValidationMessages.Failure(nameof(WorkTaskEditDto.StateId), ValidationMessages.TaskStateNotFound);
        }

        if (everyone.All(person => person.Id != task.RequestedByPersonId))
        {
            throw ValidationMessages.Failure(nameof(WorkTaskEditDto.RequestedByPersonId), ValidationMessages.TaskRequesterNotFound);
        }

        if (task.Id is { } id)
        {
            // Looked up in the owner's own partition, so a task on someone else's list cannot be reached through this one.
            WorkTask existing = await tasks.GetAsync(owner.Id, id, cancellationToken) ?? throw ValidationMessages.Failure(nameof(WorkTaskEditDto.Title), ValidationMessages.TaskNotFound);
            task.ApplyTo(existing, task.Titles()[0]);
            existing.ModifiedUtc = clock.UtcNow;

            await tasks.UpsertAsync(existing, cancellationToken);
            audit.Record(AuditEvents.TaskUpdated, AuditTargetType, existing.Id.ToString(), Describe(existing, owner));

            return [existing.Id];
        }

        List<Guid> added = [];

        foreach (string title in task.Titles())
        {
            WorkTask entity = new() { OwnerPersonId = owner.Id, CreatedUtc = clock.UtcNow };
            task.ApplyTo(entity, title);

            await tasks.UpsertAsync(entity, cancellationToken);
            audit.Record(AuditEvents.TaskCreated, AuditTargetType, entity.Id.ToString(), Describe(entity, owner));
            added.Add(entity.Id);
        }

        return added;
    }

    public async Task DeleteAsync(string? ownerEmail, Guid taskId, CancellationToken cancellationToken = default)
    {
        if (PersonMatch.ByEmail(await people.GetAllAsync(cancellationToken), ownerEmail) is not { } owner || await tasks.GetAsync(owner.Id, taskId, cancellationToken) is not { } existing)
        {
            return;
        }

        await tasks.DeleteAsync(owner.Id, taskId, cancellationToken);
        audit.Record(AuditEvents.TaskDeleted, AuditTargetType, taskId.ToString(), Describe(existing, owner));
    }

    private static string Describe(WorkTask task, Person owner) => $"{owner.DisplayName}: {task.Title}";
}
