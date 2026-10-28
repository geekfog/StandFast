using StandFast.Domain.Common;

namespace StandFast.Domain.Entities;

/// <summary>A named stretch of dates one standup's team works to, such as "Sprint 29". A standup's sprints never overlap, so any date falls in at most one.</summary>
public sealed class Sprint
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid StandupId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }

    public string? ETag { get; set; }

    public DateRange Dates => new(StartDate, EndDate);
}
