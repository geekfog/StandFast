using StandFast.Application.Dtos;
using StandFast.Application.Services;
using StandFast.Application.Tests.Fakes;
using StandFast.Domain.Entities;
using StandFast.Domain.Enums;

namespace StandFast.Application.Tests.Services;

/// <summary>
/// The board's sprint label and days off markers. The board's date is Tuesday 15 September 2026 in a sprint running to Friday 25 September, which leaves
/// nine working days counting the 15th, before holidays.
/// </summary>
public sealed class SprintAvailabilityTests
{
    private static readonly DateOnly SprintStart = new(2026, 9, 14);
    private static readonly DateOnly SprintEnd = new(2026, 9, 25);

    private readonly BoardTestContext context = new();

    [Fact]
    public async Task Board_ShowsNoSprint_WhenNoneCoversTheDate()
    {
        await context.AddMemberAsync("Ada", "Lovelace");

        StandupBoardDto board = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today);

        Assert.Null(board.Sprint);
        Assert.Empty(board.Availability);
    }

    [Fact]
    public async Task Board_ShowsTheSprintWithWorkingDaysLeft_LessFullAndHalfDayHolidays()
    {
        await context.AddMemberAsync("Ada", "Lovelace");
        await AddSprintAsync();
        await AddHolidayAsync(new DateOnly(2026, 9, 18), HolidayDuration.FullDay);
        await AddHolidayAsync(new DateOnly(2026, 9, 22), HolidayDuration.HalfDay);

        StandupBoardDto board = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today);

        Assert.Equal("Sprint 29", board.Sprint?.Name);
        Assert.Equal(7.5m, board.Sprint?.DaysLeft);
    }

    [Fact]
    public async Task Board_GivesSomeoneWithDaysOffLaterInTheSprint_TheirDaysOffAndWhatTheyHaveLeft()
    {
        Person ada = await context.AddMemberAsync("Ada", "Lovelace");
        Person ben = await context.AddMemberAsync("Ben", "Babbage");
        await AddSprintAsync();
        await AddHolidayAsync(new DateOnly(2026, 9, 22), HolidayDuration.HalfDay);
        await AddDayOffAsync(ada, new DateOnly(2026, 9, 22), new DateOnly(2026, 9, 23));

        StandupBoardDto board = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today);

        Assert.Equal(new PersonAvailabilityDto(false, null, 1.5m, 7m), board.AvailabilityFor(ada.Id));
        Assert.Null(board.AvailabilityFor(ben.Id));
    }

    [Fact]
    public async Task Board_LeavesOutDaysOffOutsideTheRestOfTheSprint_OrOnAWeekend()
    {
        Person ada = await context.AddMemberAsync("Ada", "Lovelace");
        await AddSprintAsync();
        await AddDayOffAsync(ada, SprintStart, SprintStart);
        await AddDayOffAsync(ada, new DateOnly(2026, 9, 19), new DateOnly(2026, 9, 20));

        StandupBoardDto board = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today);

        Assert.Null(board.AvailabilityFor(ada.Id));
    }

    [Fact]
    public async Task Board_MarksSomeoneOffOnTheDate_EvenWithNoSprint()
    {
        Person ada = await context.AddMemberAsync("Ada", "Lovelace");
        await AddDayOffAsync(ada, BoardTestContext.Today, BoardTestContext.Today, "Dentist");

        StandupBoardDto board = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today);

        Assert.Equal(new PersonAvailabilityDto(true, "Dentist", null, null), board.AvailabilityFor(ada.Id));
    }

    [Fact]
    public async Task MarkingSomeoneOffOnTheDate_Present_IsRefusedAndRecordsNothing()
    {
        Person ada = await context.AddMemberAsync("Ada", "Lovelace");
        await AddDayOffAsync(ada, BoardTestContext.Yesterday, BoardTestContext.Today);

        await Assert.ThrowsAsync<PersonOffException>(() => context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, ada.Id));

        StandupBoardDto board = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today);
        Assert.Equal(AttendanceState.Roster, board.Participants.Single().State);
    }

    [Fact]
    public async Task SomeoneOffOnTheDate_CanStillAttendAsAGuest()
    {
        Person ada = await context.AddPersonAsync("Ada", "Lovelace");
        await AddDayOffAsync(ada, BoardTestContext.Today, BoardTestContext.Today);

        BoardParticipantDto? guest = await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, ada.Id);

        Assert.Equal(AttendanceState.Guest, guest?.State);
    }

    private Task AddSprintAsync() =>
        context.Standups.UpsertSprintAsync(new Sprint { StandupId = context.Standup.Id, Name = "Sprint 29", StartDate = SprintStart, EndDate = SprintEnd });

    private Task AddHolidayAsync(DateOnly date, HolidayDuration duration) => context.Holidays.UpsertAsync(new Holiday { Date = date, Name = "Holiday", Duration = duration });

    private Task AddDayOffAsync(Person person, DateOnly start, DateOnly end, string? note = null) =>
        context.People.UpsertDayOffAsync(new PersonDayOff { PersonId = person.Id, StartDate = start, EndDate = end, Note = note });
}
