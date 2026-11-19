namespace StandFast.Domain.Entities;

/// <summary>A task on one person's own list, asked for by someone in People. Named to stay clear of <see cref="System.Threading.Tasks.Task"/>.</summary>
public sealed class WorkTask
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>The person whose list the task is on.</summary>
    public Guid OwnerPersonId { get; set; }

    public string Title { get; set; } = string.Empty;

    public Guid StateId { get; set; }

    /// <summary>From <see cref="Common.DomainLimits.TaskPriorityHighest"/> to <see cref="Common.DomainLimits.TaskPriorityLowest"/>.</summary>
    public int Priority { get; set; }

    public DateOnly? DueDate { get; set; }

    public Guid RequestedByPersonId { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }

    public string? ETag { get; set; }

    /// <summary>When the task last changed, which for a done task is when it was done.</summary>
    public DateTimeOffset LastUpdatedUtc => ModifiedUtc ?? CreatedUtc;
}
