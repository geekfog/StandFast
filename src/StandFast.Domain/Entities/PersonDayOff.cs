using StandFast.Domain.Common;

namespace StandFast.Domain.Entities;

/// <summary>A span of whole days one person is away, such as vacation or leave. A person's spans may overlap; a date is off when any of them covers it.</summary>
public sealed class PersonDayOff
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid PersonId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }

    public string? ETag { get; set; }

    public DateRange Dates => new(StartDate, EndDate);
}
