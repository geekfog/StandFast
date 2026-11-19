using StandFast.Domain.Enums;

namespace StandFast.Domain.Entities;

/// <summary>An organization-wide state a task can be in, such as "Blocked", named by the organization and filed under one fixed <see cref="TaskStateGroup"/>.</summary>
public sealed class TaskState
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Title { get; set; } = string.Empty;

    public TaskStateGroup Group { get; set; } = TaskStateGroup.Review;

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }

    public string? ETag { get; set; }
}
