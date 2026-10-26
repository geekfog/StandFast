using StandFast.Domain.Enums;

namespace StandFast.Domain.Entities;

/// <summary>A date the whole organization has off, in full or for half the day. At most one holiday is recorded on any date.</summary>
public sealed class Holiday
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateOnly Date { get; set; }

    public string Name { get; set; } = string.Empty;

    public HolidayDuration Duration { get; set; } = HolidayDuration.FullDay;

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }

    public string? ETag { get; set; }
}
