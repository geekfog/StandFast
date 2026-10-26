using StandFast.Domain.Common;
using StandFast.Domain.Enums;

namespace StandFast.Application.Tests.Domain;

public sealed class WorkCalendarTests
{
    /// <summary>Monday 14 September 2026 to Friday 25 September 2026: two working weeks.</summary>
    private static readonly DateRange TwoWeeks = new(new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 25));

    private static readonly Dictionary<DateOnly, HolidayDuration> NoHolidays = [];

    [Fact]
    public void WorkingDays_CountWeekdaysOnly()
    {
        Assert.Equal(10m, WorkCalendar.WorkingDays(TwoWeeks, NoHolidays));
        Assert.Equal(0m, WorkCalendar.WorkingDays(new DateRange(new DateOnly(2026, 9, 19), new DateOnly(2026, 9, 20)), NoHolidays));
    }

    [Fact]
    public void WorkingDays_TakeOffAFullOrHalfDayForEachHoliday_AndNothingForAWeekendHoliday()
    {
        Dictionary<DateOnly, HolidayDuration> holidays = new()
        {
            [new DateOnly(2026, 9, 18)] = HolidayDuration.FullDay,
            [new DateOnly(2026, 9, 22)] = HolidayDuration.HalfDay,
            [new DateOnly(2026, 9, 19)] = HolidayDuration.FullDay,
        };

        Assert.Equal(8.5m, WorkCalendar.WorkingDays(TwoWeeks, holidays));
    }

    [Fact]
    public void WorkingDays_CountNothingForADayOff_EvenOnAHalfDayHoliday()
    {
        Dictionary<DateOnly, HolidayDuration> holidays = new() { [new DateOnly(2026, 9, 22)] = HolidayDuration.HalfDay };
        DateRange off = new(new DateOnly(2026, 9, 22), new DateOnly(2026, 9, 23));

        Assert.Equal(8m, WorkCalendar.WorkingDays(TwoWeeks, holidays, off.Contains));
    }

    [Fact]
    public void DateRange_IncludesBothEnds_AndOverlapsOnASharedDay()
    {
        DateRange first = new(new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 18));

        Assert.True(first.Contains(first.Start));
        Assert.True(first.Contains(first.End));
        Assert.True(first.Overlaps(new DateRange(first.End, first.End.AddDays(7))));
        Assert.False(first.Overlaps(new DateRange(first.End.AddDays(1), first.End.AddDays(7))));
    }
}
