using StandFast.Application.Dtos;
using StandFast.Application.Tests.Fakes;
using StandFast.Domain.Entities;
using StandFast.Domain.Enums;

namespace StandFast.Application.Tests.Services;

public sealed class BoardServiceTests
{
    [Fact]
    public async Task GetBoardAsync_PutsEveryRosterMemberInTheRosterColumn()
    {
        BoardTestContext context = new();
        await context.AddMemberAsync("Ada", "Lovelace");
        await context.AddMemberAsync("Grace", "Hopper");

        StandupBoardDto board = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today);

        Assert.Equal(2, board.Participants.Count);
        Assert.All(board.Participants, participant => Assert.Equal(AttendanceState.Roster, participant.State));
    }

    [Theory]
    [InlineData(1, AttendanceState.Available)]
    [InlineData(2, AttendanceState.Presented)]
    [InlineData(3, AttendanceState.Presented)]
    public async Task AdvanceAsync_MovesThroughTheColumnsAndStopsAtPresented(int taps, AttendanceState expected)
    {
        BoardTestContext context = new();
        Person person = await context.AddMemberAsync("Ada", "Lovelace");

        BoardParticipantDto? participant = null;
        for (int tap = 0; tap < taps; tap++)
        {
            participant = await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, person.Id);
        }

        Assert.Equal(expected, participant!.State);
        Assert.Equal(BoardTestContext.Now, participant.MarkedAvailableUtc);
    }

    [Fact]
    public async Task RevertAsync_UndoesTheLastTapAndClearsThePresentedTimestamp()
    {
        BoardTestContext context = new();
        Person person = await context.AddMemberAsync("Ada", "Lovelace");

        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, person.Id);
        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, person.Id);
        BoardParticipantDto? participant = await context.Service.RevertAsync(context.Standup.Id, BoardTestContext.Today, person.Id);

        Assert.Equal(AttendanceState.Available, participant!.State);
        Assert.Null(participant.PresentedUtc);
    }

    [Fact]
    public async Task GetBoardAsync_SurfacesTheMostRecentEarlierEntryAsThePriorUpdate()
    {
        BoardTestContext context = new();
        Person person = await context.AddMemberAsync("Ada", "Lovelace");

        await context.Entries.UpsertAsync(new StandupEntry
        {
            StandupId = context.Standup.Id,
            PersonId = person.Id,
            MeetingDate = BoardTestContext.Yesterday,
            Update = "Shipped the analytics endpoint.",
            UpdateSavedUtc = BoardTestContext.Now.AddDays(-1),
        });

        StandupBoardDto board = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today);
        BoardParticipantDto participant = Assert.Single(board.Participants);

        Assert.Equal("Shipped the analytics endpoint.", participant.PriorUpdate);
        Assert.Equal(BoardTestContext.Yesterday, participant.PriorMeetingDate);
        Assert.True(participant.HasPrior);
        Assert.Null(participant.Update);
    }

    [Fact]
    public async Task SaveUpdateAsync_StoresTheMarkdownAndStampsTheSaveTime()
    {
        BoardTestContext context = new();
        Person person = await context.AddMemberAsync("Ada", "Lovelace");

        BoardParticipantDto? participant = await context.Service.SaveUpdateAsync(new ParticipantUpdateDto
        {
            StandupId = context.Standup.Id,
            PersonId = person.Id,
            MeetingDate = BoardTestContext.Today,
            Update = "  **Done** the migration.  ",
            Blockers = "   ",
        });

        Assert.Equal("**Done** the migration.", participant!.Update);
        Assert.Null(participant.Blockers);
        Assert.Equal(BoardTestContext.Now, participant.UpdateSavedUtc);
    }

    [Fact]
    public async Task SaveUpdateAsync_IgnoresSomeoneWhoIsNotOnTheRoster()
    {
        BoardTestContext context = new();
        await context.AddMemberAsync("Ada", "Lovelace");

        BoardParticipantDto? participant = await context.Service.SaveUpdateAsync(new ParticipantUpdateDto
        {
            StandupId = context.Standup.Id,
            PersonId = Guid.CreateVersion7(),
            MeetingDate = BoardTestContext.Today,
            Update = "Not my standup.",
        });

        Assert.Null(participant);
    }
}
