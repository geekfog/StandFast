using StandFast.Domain.Enums;

namespace StandFast.Application.Dtos;

public sealed record TaskStateDto(Guid Id, string Title, TaskStateGroup Group);

/// <summary>Create/update input for a task state. <see cref="Id"/> is null when creating.</summary>
public sealed class TaskStateEditDto
{
    public Guid? Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public TaskStateGroup Group { get; set; } = TaskStateGroup.Review;
}

/// <summary>One task on a person's list.</summary>
/// <param name="StateTitle">Null when the task's state is no longer defined, which only a restore of older data can leave behind.</param>
/// <param name="LastUpdatedUtc">When the task last changed, which for a done task is when it was done.</param>
public sealed record WorkTaskDto(Guid Id, string Title, Guid StateId, string? StateTitle, TaskStateGroup? StateGroup, int Priority, DateOnly? DueDate,
    Guid RequestedByPersonId, DateTimeOffset CreatedUtc, DateTimeOffset LastUpdatedUtc)
{
    public bool IsDone => StateGroup?.IsDone() == true;
}

/// <summary>Create/update input for a task. <see cref="Id"/> is null when creating; the other nullable values are null until picked, and saving requires all but the due date.</summary>
public sealed class WorkTaskEditDto
{
    public Guid? Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public Guid? StateId { get; set; }

    public int? Priority { get; set; }

    public DateOnly? DueDate { get; set; }

    public Guid? RequestedByPersonId { get; set; }
}

/// <summary>The signed-in user's own task list.</summary>
/// <param name="OwnerPersonId">The person in People whose address the signed-in user has, which a new task's Requested By defaults to.</param>
/// <param name="Tasks">Every task on the list in list order: open tasks first, then done ones.</param>
public sealed record TaskListDto(Guid OwnerPersonId, IReadOnlyList<WorkTaskDto> Tasks);
