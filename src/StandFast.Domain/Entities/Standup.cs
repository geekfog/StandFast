using StandFast.Domain.Enums;

namespace StandFast.Domain.Entities;

/// <summary>A recurring daily scrum definition. The board is always scoped to one standup plus one date.</summary>
public sealed class Standup
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public MeetingDays RecurrenceDays { get; set; } = MeetingDays.Weekdays;

    public TimeOnly StartTimeLocal { get; set; } = new(9, 0);

    /// <summary>IANA or Windows time zone id the start time is expressed in.</summary>
    public string TimeZoneId { get; set; } = TimeZoneInfo.Local.Id;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }

    public string? ETag { get; set; }

    public bool OccursOn(DateOnly date) => RecurrenceDays.Includes(date.DayOfWeek);
}
