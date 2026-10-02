using StandFast.Application.Dtos;
using StandFast.Application.Tests.Fakes;
using StandFast.Domain.Entities;
using StandFast.Domain.Enums;

namespace StandFast.Application.Tests.Services;

public sealed class GuestAttendanceTests
{
    [Fact]
    public async Task GetBoardAsync_OffersEveryActiveNonPresenterAsAGuest()
    {
        BoardTestContext context = new();
        Person presenter = await context.AddMemberAsync("Ada", "Lovelace");
        Person visitor = await context.AddPersonAsync("Grace", "Hopper");
        await context.AddPersonAsync("Alan", "Turing", isActive: false);

        StandupBoardDto board = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today);

        Assert.Equal(AttendeeKind.Presenter, Assert.Single(board.Participants, participant => participant.PersonId == presenter.Id).Kind);
        BoardParticipantDto guest = Assert.Single(board.Participants, participant => participant.Kind == AttendeeKind.Guest);
        Assert.Equal(visitor.Id, guest.PersonId);
        Assert.Equal(AttendanceState.Roster, guest.State);
    }

    [Fact]
    public async Task AdvanceAsync_MakesANonPresenterAGuestWhoGoesNoFurther()
    {
        BoardTestContext context = new();
        Person visitor = await context.AddPersonAsync("Grace", "Hopper");

        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, visitor.Id);
        BoardParticipantDto? guest = await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, visitor.Id);

        Assert.Equal(AttendanceState.Guest, guest!.State);
        Assert.Equal(BoardTestContext.Now, guest.MarkedAvailableUtc);
        Assert.Null(guest.PresentedUtc);
    }

    [Fact]
    public async Task RevertAsync_ReturnsAGuestToTheRoster()
    {
        BoardTestContext context = new();
        Person visitor = await context.AddPersonAsync("Grace", "Hopper");

        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, visitor.Id);
        BoardParticipantDto? participant = await context.Service.RevertAsync(context.Standup.Id, BoardTestContext.Today, visitor.Id);

        Assert.Equal(AttendanceState.Roster, participant!.State);
        Assert.Null(participant.MarkedAvailableUtc);
    }

    [Fact]
    public async Task AdvanceAsync_NeverMakesAPresenterAGuest()
    {
        BoardTestContext context = new();
        Person presenter = await context.AddMemberAsync("Ada", "Lovelace");

        BoardParticipantDto? participant = await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, presenter.Id);

        Assert.Equal(AttendanceState.Available, participant!.State);
    }

    [Fact]
    public async Task GetBoardAsync_ReadsAGuestEntryFromBeforeJoiningThePresenterRosterAsNotYetSeen()
    {
        BoardTestContext context = new();
        Person person = await context.AddPersonAsync("Grace", "Hopper");
        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, person.Id);
        await context.AddMemberAsync(person, RosterRole.Presenter);

        StandupBoardDto board = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today);

        BoardParticipantDto participant = Assert.Single(board.Participants);
        Assert.Equal(AttendeeKind.Presenter, participant.Kind);
        Assert.Equal(AttendanceState.Roster, participant.State);
    }

    [Fact]
    public async Task SaveUpdateAsync_RecordsNothingForAGuest()
    {
        BoardTestContext context = new();
        Person visitor = await context.AddPersonAsync("Grace", "Hopper");
        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, visitor.Id);

        BoardParticipantDto? saved = await context.Service.SaveUpdateAsync(new ParticipantUpdateDto
        {
            StandupId = context.Standup.Id,
            PersonId = visitor.Id,
            MeetingDate = BoardTestContext.Today,
            Update = "Just listening in.",
        });

        Assert.Null(saved);
    }

    [Fact]
    public async Task SetLeaderAsync_RefusesALeaderWhoIsNotAttending()
    {
        BoardTestContext context = new();
        Person leader = await context.AddMemberAsync("Grace", "Hopper", RosterRole.Leader);

        bool recorded = await context.Service.SetLeaderAsync(context.Standup.Id, BoardTestContext.Today, leader.Id);

        Assert.False(recorded);
    }

    [Fact]
    public async Task SetLeaderAsync_AcceptsALeaderAttendingAsAGuest()
    {
        BoardTestContext context = new();
        Person leader = await context.AddMemberAsync("Grace", "Hopper", RosterRole.Leader);
        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, leader.Id);

        bool recorded = await context.Service.SetLeaderAsync(context.Standup.Id, BoardTestContext.Today, leader.Id);

        Assert.True(recorded);
        Assert.Equal(leader.Id, (await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today)).LeaderPersonId);
    }

    [Fact]
    public async Task SetLeaderAsync_AcceptsALeaderWhoIsPresentAsAPresenter()
    {
        BoardTestContext context = new();
        Person leader = await context.AddMemberAsync("Ada", "Lovelace");
        await context.AddMemberAsync(leader, RosterRole.Leader);
        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, leader.Id);

        Assert.True(await context.Service.SetLeaderAsync(context.Standup.Id, BoardTestContext.Today, leader.Id));
    }

    [Fact]
    public async Task LeaderCandidates_ListsOnlyLeadersWhoAreAttending()
    {
        BoardTestContext context = new();
        Person attending = await context.AddMemberAsync("Grace", "Hopper", RosterRole.Leader);
        await context.AddMemberAsync("Alan", "Turing", RosterRole.Leader);
        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, attending.Id);

        StandupBoardDto board = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today);

        Assert.Equal(attending.Id, Assert.Single(board.LeaderCandidates).PersonId);
    }

    [Fact]
    public async Task RevertAsync_ClearsTheLeaderWhenTheyStopAttending()
    {
        BoardTestContext context = new();
        Person leader = await context.AddMemberAsync("Grace", "Hopper", RosterRole.Leader);
        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, leader.Id);
        await context.Service.SetLeaderAsync(context.Standup.Id, BoardTestContext.Today, leader.Id);
        StandupBoardDto before = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today);

        BoardParticipantDto? reverted = await context.Service.RevertAsync(context.Standup.Id, BoardTestContext.Today, leader.Id);

        Assert.Null((await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today)).LeaderPersonId);
        Assert.Null(before.WithParticipant(reverted!).LeaderPersonId);
    }
}
