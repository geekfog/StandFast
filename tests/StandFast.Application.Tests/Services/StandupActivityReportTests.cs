using StandFast.Application.Dtos;
using StandFast.Application.Reporting;
using StandFast.Application.Tests.Fakes;
using StandFast.Domain.Entities;

namespace StandFast.Application.Tests.Services;

public sealed class StandupActivityReportTests
{
    private readonly ReportTestContext context = new();

    [Fact]
    public async Task ListsTheDaysThatRanNewestFirstWithTheirPresenterCounts()
    {
        Person amara = await context.AddPersonAsync("Amara", "Okafor");
        Person ben = await context.AddPersonAsync("Ben", "Ruiz");

        DateOnly firstMeeting = ReportTestContext.Today.AddDays(-2);
        DateOnly secondMeeting = ReportTestContext.Today;

        await context.PresentedAsync(amara, firstMeeting, 0);
        await context.PresentedAsync(ben, secondMeeting, 0);
        await context.PresentedAsync(amara, secondMeeting, 4);

        StandupActivityDto activity = await RunAsync();

        Assert.Equal([secondMeeting, firstMeeting], activity.Days.Select(day => day.MeetingDate));
        Assert.Equal([2, 1], activity.Days.Select(day => day.PresenterCount));
    }

    [Fact]
    public async Task MeasuresTheStandupFromItsFirstTurnToTheLock()
    {
        Person amara = await context.AddPersonAsync("Amara", "Okafor");
        Person ben = await context.AddPersonAsync("Ben", "Ruiz");

        await context.PresentedAsync(amara, ReportTestContext.Today, 2);
        await context.PresentedAsync(ben, ReportTestContext.Today, 9);
        await context.LockedAsync(ReportTestContext.Today, ReportTestContext.NineAm.AddMinutes(14));

        StandupActivityDayDto day = Assert.Single((await RunAsync()).Days);

        Assert.True(day.IsLocked);
        Assert.Equal(12, day.DurationMinutes);
    }

    [Fact]
    public async Task LeavesTheDurationUnansweredWhileTheDayIsStillOpen()
    {
        Person amara = await context.AddPersonAsync("Amara", "Okafor");
        await context.PresentedAsync(amara, ReportTestContext.Today, 0);

        StandupActivityDayDto day = Assert.Single((await RunAsync()).Days);

        Assert.False(day.IsLocked);
        Assert.Null(day.DurationMinutes);
    }

    [Fact]
    public async Task LeavesOutADayThatWasAnnotatedButNeverPresentedOn()
    {
        Person amara = await context.AddPersonAsync("Amara", "Okafor");

        await context.AttendedWithoutPresentingAsync(amara, ReportTestContext.Today);
        await context.LedByAsync(ReportTestContext.Today, amara);

        StandupActivityDto activity = await RunAsync();

        Assert.False(activity.HasData);
        Assert.Equal(context.Standup.Name, activity.StandupName);
    }

    [Fact]
    public async Task LeavesOutADayOutsideThePeriod()
    {
        Person amara = await context.AddPersonAsync("Amara", "Okafor");

        await context.PresentedAsync(amara, ReportTestContext.Today.AddDays(-45), 0);
        await context.PresentedAsync(amara, ReportTestContext.Today, 0);

        StandupActivityDto activity = await RunAsync(ReportWindow.OneMonth.Days);

        Assert.Equal([ReportTestContext.Today], activity.Days.Select(day => day.MeetingDate));
    }

    [Fact]
    public async Task IsEmptyForAStandupThatDoesNotExist()
    {
        StandupActivityDto activity = await context.Service.GetStandupActivityAsync(Guid.CreateVersion7(), ReportWindow.Default.Days, context.TimeZone);

        Assert.False(activity.HasData);
    }

    private Task<StandupActivityDto> RunAsync(int? days = null) =>
        context.Service.GetStandupActivityAsync(context.Standup.Id, days ?? ReportWindow.Default.Days, context.TimeZone);
}
