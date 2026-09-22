using StandFast.Application.Dtos;
using StandFast.Application.Reporting;
using StandFast.Application.Tests.Fakes;
using StandFast.Domain.Entities;

namespace StandFast.Application.Tests.Services;

public sealed class ReportServiceTests
{
    private readonly ReportTestContext context = new();

    [Fact]
    public async Task PresentingOrderTimeline_RanksEachMeetingSeparately()
    {
        Person amara = await context.AddPersonAsync("Amara", "Okafor");
        Person ben = await context.AddPersonAsync("Ben", "Ruiz");

        DateOnly firstMeeting = ReportTestContext.Today.AddDays(-2);
        DateOnly secondMeeting = ReportTestContext.Today;

        await context.PresentedAsync(amara, firstMeeting, 0);
        await context.PresentedAsync(ben, firstMeeting, 3);
        await context.PresentedAsync(ben, secondMeeting, 0);
        await context.PresentedAsync(amara, secondMeeting, 4);

        PresentingOrderTimelineDto timeline = await RunAsync();

        Assert.Equal([firstMeeting, secondMeeting], timeline.MeetingDates);
        Assert.Equal([1, 2], PresenterNamed(timeline, amara).OrdersByDate);
        Assert.Equal([2, 1], PresenterNamed(timeline, ben).OrdersByDate);
        Assert.Equal(2, timeline.HighestOrder);
    }

    [Fact]
    public async Task PresentingOrderTimeline_LeavesTheOrderUnsetOnADayAPersonDidNotPresent()
    {
        Person amara = await context.AddPersonAsync("Amara", "Okafor");
        Person ben = await context.AddPersonAsync("Ben", "Ruiz");

        DateOnly firstMeeting = ReportTestContext.Today.AddDays(-1);

        await context.PresentedAsync(amara, firstMeeting, 0);
        await context.PresentedAsync(ben, firstMeeting, 2);
        await context.PresentedAsync(amara, ReportTestContext.Today, 0);
        await context.AttendedWithoutPresentingAsync(ben, ReportTestContext.Today);

        PresentingOrderTimelineDto timeline = await RunAsync();

        Assert.Equal([2, null], PresenterNamed(timeline, ben).OrdersByDate);
    }

    [Fact]
    public async Task PresentingOrderTimeline_ExcludesAnyoneWhoNeverPresentedInThePeriod()
    {
        Person amara = await context.AddPersonAsync("Amara", "Okafor");
        Person ben = await context.AddPersonAsync("Ben", "Ruiz");

        await context.PresentedAsync(amara, ReportTestContext.Today, 0);
        await context.AttendedWithoutPresentingAsync(ben, ReportTestContext.Today);

        PresentingOrderTimelineDto timeline = await RunAsync();

        Assert.Equal(amara.DisplayName, Assert.Single(timeline.Presenters).DisplayName);
    }

    [Fact]
    public async Task PresentingOrderTimeline_CountsTodayAsTheLastDayOfTheWindow()
    {
        Person amara = await context.AddPersonAsync("Amara", "Okafor");

        DateOnly firstDayInWindow = ReportTestContext.Today.AddDays(-(ReportWindow.Default.Days - 1));

        await context.PresentedAsync(amara, firstDayInWindow, 0);
        await context.PresentedAsync(amara, firstDayInWindow.AddDays(-1), 0);

        PresentingOrderTimelineDto timeline = await RunAsync();

        Assert.Equal(firstDayInWindow, timeline.From);
        Assert.Equal(ReportTestContext.Today, timeline.To);
        Assert.Equal([firstDayInWindow], timeline.MeetingDates);
    }

    [Fact]
    public async Task PresentingOrderTimeline_ReportsNoDataWhenNobodyPresented()
    {
        Person amara = await context.AddPersonAsync("Amara", "Okafor");
        await context.AttendedWithoutPresentingAsync(amara, ReportTestContext.Today);

        PresentingOrderTimelineDto timeline = await RunAsync();

        Assert.False(timeline.HasData);
        Assert.Equal(context.Standup.Name, timeline.StandupName);
    }

    [Fact]
    public async Task PresentingOrderTimeline_IsEmptyForAStandupThatDoesNotExist()
    {
        PresentingOrderTimelineDto timeline = await context.Service.GetPresentingOrderTimelineAsync(
            Guid.CreateVersion7(), ReportWindow.Default.Days, context.TimeZone);

        Assert.False(timeline.HasData);
    }

    private Task<PresentingOrderTimelineDto> RunAsync(int? days = null) =>
        context.Service.GetPresentingOrderTimelineAsync(context.Standup.Id, days ?? ReportWindow.Default.Days, context.TimeZone);

    private static PresenterTimelineDto PresenterNamed(PresentingOrderTimelineDto timeline, Person person) =>
        timeline.Presenters.Single(presenter => presenter.PersonId == person.Id);
}
