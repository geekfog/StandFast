using StandFast.Application.Dtos;
using StandFast.Application.Reporting;
using StandFast.Application.Tests.Fakes;
using StandFast.Domain.Entities;

namespace StandFast.Application.Tests.Services;

public sealed class AttendanceReportTests
{
    private readonly ReportTestContext context = new();

    [Fact]
    public async Task CountsMissedDaysOnlyFromWhenThePersonJoinedTheRoster()
    {
        Person amara = await context.AddPersonAsync("Amara", "Okafor");
        Person ben = await context.AddPersonAsync("Ben", "Ruiz");

        DateOnly first = ReportTestContext.Today.AddDays(-2);
        DateOnly second = ReportTestContext.Today.AddDays(-1);
        DateOnly third = ReportTestContext.Today;

        await context.OnRosterAsync(amara, first.AddDays(-10));
        await context.OnRosterAsync(ben, second);

        await context.PresentedAsync(amara, first, 0);
        await context.PresentedAsync(amara, second, 0);
        await context.PresentedAsync(ben, third, 0);

        AttendanceDto report = await RunAsync();
        PersonAttendanceDto amaraRow = Row(report, amara);
        PersonAttendanceDto benRow = Row(report, ben);

        Assert.Equal(3, report.MeetingCount);
        Assert.Equal((2, 1), (amaraRow.Presented, amaraRow.Missed));
        Assert.Equal((1, 1), (benRow.Presented, benRow.Missed));
        Assert.Equal(0.5, benRow.AttendanceRate);
    }

    [Fact]
    public async Task ListsARosterMemberWhoNeverPresentedAsMissingEveryDay()
    {
        Person amara = await context.AddPersonAsync("Amara", "Okafor");
        Person ben = await context.AddPersonAsync("Ben", "Ruiz");

        await context.OnRosterAsync(ben, ReportTestContext.Today.AddDays(-10));
        await context.PresentedAsync(amara, ReportTestContext.Today, 0);

        PersonAttendanceDto benRow = Row(await RunAsync(), ben);

        Assert.Equal((0, 1), (benRow.Presented, benRow.Missed));
        Assert.Equal(0, benRow.AttendanceRate);
        Assert.Null(benRow.AverageTurn);
    }

    [Fact]
    public async Task LeavesMissedUnansweredForSomeoneNoLongerOnTheRoster()
    {
        Person amara = await context.AddPersonAsync("Amara", "Okafor");
        await context.PresentedAsync(amara, ReportTestContext.Today, 0);

        PersonAttendanceDto row = Row(await RunAsync(), amara);

        Assert.Equal(1, row.Presented);
        Assert.Null(row.Missed);
        Assert.Null(row.AttendanceRate);
    }

    [Fact]
    public async Task TimesEachTurnToTheNextAndTheLastTurnToTheLock()
    {
        Person amara = await context.AddPersonAsync("Amara", "Okafor");
        Person ben = await context.AddPersonAsync("Ben", "Ruiz");

        DateOnly earlier = ReportTestContext.Today.AddDays(-1);
        await context.PresentedAsync(amara, earlier, 0);
        await context.PresentedAsync(ben, earlier, 2);
        await context.LockedAsync(earlier, ReportTestContext.NineAm.AddDays(-1).AddMinutes(7));

        await context.PresentedAsync(ben, ReportTestContext.Today, 0);
        await context.PresentedAsync(amara, ReportTestContext.Today, 1);
        await context.LockedAsync(ReportTestContext.Today, ReportTestContext.NineAm.AddMinutes(5));

        AttendanceDto report = await RunAsync();
        PersonAttendanceDto amaraRow = Row(report, amara);
        PersonAttendanceDto benRow = Row(report, ben);

        Assert.Equal(TimeSpan.FromMinutes(2), amaraRow.ShortestTurn);
        Assert.Equal(TimeSpan.FromMinutes(4), amaraRow.LongestTurn);
        Assert.Equal(TimeSpan.FromMinutes(3), amaraRow.AverageTurn);
        Assert.Equal(TimeSpan.FromMinutes(1), benRow.ShortestTurn);
        Assert.Equal(TimeSpan.FromMinutes(5), benRow.LongestTurn);
    }

    [Fact]
    public async Task LeavesOutTheLastTurnOfADayStillOpen()
    {
        Person amara = await context.AddPersonAsync("Amara", "Okafor");
        Person ben = await context.AddPersonAsync("Ben", "Ruiz");

        await context.PresentedAsync(amara, ReportTestContext.Today, 0);
        await context.PresentedAsync(ben, ReportTestContext.Today, 3);

        AttendanceDto report = await RunAsync();

        Assert.Equal([TimeSpan.FromMinutes(3)], Row(report, amara).TurnLengths);
        Assert.Empty(Row(report, ben).TurnLengths);
    }

    [Fact]
    public async Task IsEmptyWhenNobodyPresentedInThePeriod()
    {
        Person amara = await context.AddPersonAsync("Amara", "Okafor");
        await context.OnRosterAsync(amara, ReportTestContext.Today.AddDays(-10));

        AttendanceDto report = await RunAsync();

        Assert.False(report.HasData);
        Assert.Equal(context.Standup.Name, report.StandupName);
    }

    private static PersonAttendanceDto Row(AttendanceDto report, Person person) => Assert.Single(report.People, row => row.PersonId == person.Id);

    private Task<AttendanceDto> RunAsync() => context.Service.GetAttendanceAsync(context.Standup.Id, ReportWindow.Default.Days, context.TimeZone);
}
