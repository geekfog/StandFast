using StandFast.Domain.Enums;

namespace StandFast.Domain.Common;

/// <summary>How many working days a span of dates holds, after weekends, holidays, and anyone's own time off.</summary>
public static class WorkCalendar
{
    /// <summary>Days of the week that count as working days, whatever days a standup happens to run on.</summary>
    public const MeetingDays BusinessDays = MeetingDays.Weekdays;

    public const decimal FullDay = 1m;

    public const decimal HalfDay = FullDay / 2;

    /// <summary>
    /// Working days in <paramref name="range"/>: each business day counts as a full day, less what a holiday on it takes off. A date for which
    /// <paramref name="isOff"/> is true counts as nothing, since someone off for the day contributes none of it.
    /// </summary>
    public static decimal WorkingDays(DateRange range, IReadOnlyDictionary<DateOnly, HolidayDuration> holidays, Func<DateOnly, bool>? isOff = null) =>
        range.Dates()
            .Where(date => BusinessDays.Includes(date.DayOfWeek) && isOff?.Invoke(date) != true)
            .Sum(date => FullDay - (holidays.TryGetValue(date, out HolidayDuration duration) ? duration.DaysOff() : 0m));
}
