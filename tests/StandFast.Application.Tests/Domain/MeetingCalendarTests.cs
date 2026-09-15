using StandFast.Domain.Common;
using StandFast.Domain.Enums;

namespace StandFast.Application.Tests.Domain;

public sealed class MeetingCalendarTests
{
    [Theory]
    [InlineData("2026-09-15", "2026-09-14")] // Tuesday resolves back to Monday.
    [InlineData("2026-09-14", "2026-09-14")] // Monday is already the start of the week.
    [InlineData("2026-09-20", "2026-09-14")] // Sunday belongs to the week that started the previous Monday.
    public void StartOfWeek_AlwaysReturnsTheMondayOfThatWeek(string date, string expected)
    {
        DateOnly start = MeetingCalendar.StartOfWeek(DateOnly.Parse(date));

        Assert.Equal(DateOnly.Parse(expected), start);
    }

    [Fact]
    public void Week_ReturnsSevenConsecutiveDaysStartingOnMonday()
    {
        IReadOnlyList<DateOnly> week = MeetingCalendar.Week(new DateOnly(2026, 9, 17));

        Assert.Equal(MeetingCalendar.DaysPerWeek, week.Count);
        Assert.Equal(DayOfWeek.Monday, week[0].DayOfWeek);
        Assert.Equal(new DateOnly(2026, 9, 20), week[^1]);
    }

    [Fact]
    public void RouteValue_RoundTrips()
    {
        DateOnly date = new(2026, 9, 15);

        Assert.Equal(date, MeetingCalendar.ParseRouteValue(MeetingCalendar.ToRouteValue(date)));
        Assert.Null(MeetingCalendar.ParseRouteValue("not-a-date"));
    }
}

public sealed class AttendanceTransitionTests
{
    [Theory]
    [InlineData(AttendanceState.Roster, AttendanceState.Available)]
    [InlineData(AttendanceState.Available, AttendanceState.Presented)]
    [InlineData(AttendanceState.Presented, AttendanceState.Presented)]
    public void Advance_MovesOneColumnRight(AttendanceState from, AttendanceState expected) => Assert.Equal(expected, AttendanceTransitions.Advance(from));

    [Theory]
    [InlineData(AttendanceState.Presented, AttendanceState.Available)]
    [InlineData(AttendanceState.Available, AttendanceState.Roster)]
    [InlineData(AttendanceState.Roster, AttendanceState.Roster)]
    public void Revert_MovesOneColumnLeft(AttendanceState from, AttendanceState expected) => Assert.Equal(expected, AttendanceTransitions.Revert(from));
}

public sealed class MeetingDaysTests
{
    [Fact]
    public void Weekdays_CoversMondayToFridayOnly()
    {
        Assert.True(MeetingDays.Weekdays.Includes(DayOfWeek.Monday));
        Assert.True(MeetingDays.Weekdays.Includes(DayOfWeek.Friday));
        Assert.False(MeetingDays.Weekdays.Includes(DayOfWeek.Saturday));
        Assert.False(MeetingDays.Weekdays.Includes(DayOfWeek.Sunday));
    }

    [Fact]
    public void With_TogglesASingleDay()
    {
        MeetingDays days = MeetingDays.Weekdays.With(DayOfWeek.Saturday, included: true).With(DayOfWeek.Monday, included: false);

        Assert.True(days.Includes(DayOfWeek.Saturday));
        Assert.False(days.Includes(DayOfWeek.Monday));
    }

    [Fact]
    public void InDisplayOrder_StartsOnTheConfiguredFirstDayOfWeek()
    {
        Assert.Equal(MeetingCalendar.FirstDayOfWeek, MeetingDaysExtensions.InDisplayOrder[0]);
        Assert.Equal(MeetingCalendar.DaysPerWeek, MeetingDaysExtensions.InDisplayOrder.Count);
    }
}
