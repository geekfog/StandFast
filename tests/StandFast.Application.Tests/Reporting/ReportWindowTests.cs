using StandFast.Application.Reporting;

namespace StandFast.Application.Tests.Reporting;

public sealed class ReportWindowTests
{
    private static readonly DateOnly Today = new(2026, 9, 15);

    [Fact]
    public void ResolveDays_FallsBackToTheDefaultWindow()
    {
        Assert.Equal(ReportWindow.Default.Days, ReportWindow.ResolveDays(null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-90)]
    public void ResolveDays_ClampsAnythingShorterThanTheShortestWindow(int requested)
    {
        Assert.Equal(ReportWindow.MinimumDays, ReportWindow.ResolveDays(requested));
    }

    [Fact]
    public void ResolveDays_ClampsAnythingLongerThanTheLongestWindow()
    {
        Assert.Equal(ReportWindow.MaximumDays, ReportWindow.ResolveDays(int.MaxValue));
    }

    [Fact]
    public void ResolveDays_KeepsADayCountBetweenTheOfferedWindows()
    {
        Assert.Equal(45, ReportWindow.ResolveDays(45));
    }

    [Fact]
    public void RangeEndingOn_CountsTodayAsOneOfTheDays()
    {
        (DateOnly from, DateOnly to) = ReportWindow.RangeEndingOn(Today, 30);

        Assert.Equal(Today, to);
        Assert.Equal(30, to.DayNumber - from.DayNumber + 1);
    }
}
