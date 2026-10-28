using StandFast.Domain.Common;
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

    /// <summary>Time zone id the start time is expressed in, in the form <see cref="TimeZoneIds"/> stores.</summary>
    public string TimeZoneId { get; set; } = TimeZoneIds.Local;

    public bool IsActive { get; set; } = true;

    /// <summary>True when leaders who are not on the presenter roster can be marked as presented after attending.</summary>
    public bool LeadersCanPresent { get; set; }

    /// <summary>True when guests, rostered or visiting, can be marked as presented after attending.</summary>
    public bool GuestsCanPresent { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }

    public string? ETag { get; set; }

    public bool OccursOn(DateOnly date) => RecurrenceDays.Includes(date.DayOfWeek);

    /// <summary>True when someone of <paramref name="kind"/> gives an update at this standup. Presenters always do; leaders and guests only where it allows them to.</summary>
    public bool Presents(AttendeeKind kind) => kind switch
    {
        AttendeeKind.Leader => LeadersCanPresent,
        AttendeeKind.Guest => GuestsCanPresent,
        _ => true,
    };
}
