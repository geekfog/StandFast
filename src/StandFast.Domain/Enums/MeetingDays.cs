using StandFast.Domain.Common;

namespace StandFast.Domain.Enums;

/// <summary>Days a standup recurs on. Flags so the whole schedule stores as a single integer property.</summary>
[Flags]
public enum MeetingDays
{
    None = 0,
    Sunday = 1 << 0,
    Monday = 1 << 1,
    Tuesday = 1 << 2,
    Wednesday = 1 << 3,
    Thursday = 1 << 4,
    Friday = 1 << 5,
    Saturday = 1 << 6,
    Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
    EveryDay = Weekdays | Saturday | Sunday,
}

public static class MeetingDaysExtensions
{
    public static MeetingDays ToMeetingDay(this DayOfWeek day) => (MeetingDays)(1 << (int)day);

    public static bool Includes(this MeetingDays days, DayOfWeek day) => days.HasFlag(day.ToMeetingDay());

    public static MeetingDays With(this MeetingDays days, DayOfWeek day, bool included) =>
        included ? days | day.ToMeetingDay() : days & ~day.ToMeetingDay();

    /// <summary>Days of the week in display order, starting on <see cref="MeetingCalendar.FirstDayOfWeek"/>. Shared by the schedule editor and the week strip.</summary>
    public static IReadOnlyList<DayOfWeek> InDisplayOrder { get; } =
    [
        .. Enumerable.Range(0, MeetingCalendar.DaysPerWeek).Select(offset => (DayOfWeek)(((int)MeetingCalendar.FirstDayOfWeek + offset) % MeetingCalendar.DaysPerWeek)),
    ];
}
