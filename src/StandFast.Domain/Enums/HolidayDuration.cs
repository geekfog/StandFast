using StandFast.Domain.Common;

namespace StandFast.Domain.Enums;

/// <summary>How much of a business day a holiday takes off.</summary>
public enum HolidayDuration
{
    FullDay = 0,
    HalfDay = 1,
}

public static class HolidayDurationExtensions
{
    /// <summary>Durations in the order screens list them.</summary>
    public static IReadOnlyList<HolidayDuration> InDisplayOrder { get; } = [HolidayDuration.FullDay, HolidayDuration.HalfDay];

    /// <summary>The share of a business day the holiday takes off.</summary>
    public static decimal DaysOff(this HolidayDuration duration) => duration == HolidayDuration.HalfDay ? WorkCalendar.HalfDay : WorkCalendar.FullDay;
}
