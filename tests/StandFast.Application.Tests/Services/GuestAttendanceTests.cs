using StandFast.Application.Dtos;
using StandFast.Application.Tests.Fakes;
using StandFast.Domain.Entities;
using StandFast.Domain.Enums;

namespace StandFast.Application.Tests.Services;

public sealed class GuestAttendanceTests
{
    [Fact]
    public async Task GetBoardAsync_OffersEachRosterUnderItsOwnKind_AndNobodyOnNoRoster()
    {
        BoardTestContext context = new();
        Person presenter = await context.AddMemberAsync("Ada", "Lovelace");
        Person leader = await context.AddMemberAsync("Grace", "Hopper", RosterRole.Leader);
        Person guest = await context.AddMemberAsync("Alan", "Turing", RosterRole.Guest);
        Person unassigned = await context.AddPersonAsync("Edsger", "Dijkstra");

        StandupBoardDto board = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today);

        Assert.Equal(AttendeeKind.Presenter, Assert.Single(board.Participants, participant => participant.PersonId == presenter.Id).Kind);
        Assert.Equal(AttendeeKind.Leader, Assert.Single(board.Participants, participant => participant.PersonId == leader.Id).Kind);
        Assert.Equal(AttendeeKind.Guest, Assert.Single(board.Participants, participant => participant.PersonId == guest.Id).Kind);
        Assert.DoesNotContain(board.Participants, participant => participant.PersonId == unassigned.Id);
        Assert.All(board.Participants, participant => Assert.Equal(AttendanceState.Roster, participant.State));
    }

    [Fact]
    public async Task GetBoardAsync_TakesTheFirstOfPresenterLeaderAndGuestForSomeoneOnSeveralRosters()
    {
        BoardTestContext context = new();
        Person presenter = await context.AddMemberAsync("Ada", "Lovelace");
        await context.AddMemberAsync(presenter, RosterRole.Leader);
        Person leader = await context.AddMemberAsync("Grace", "Hopper", RosterRole.Guest);
        await context.AddMemberAsync(leader, RosterRole.Leader);

        StandupBoardDto board = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today);

        Assert.Equal(AttendeeKind.Presenter, Assert.Single(board.Participants, participant => participant.PersonId == presenter.Id).Kind);
        Assert.Equal(AttendeeKind.Leader, Assert.Single(board.Participants, participant => participant.PersonId == leader.Id).Kind);
    }

    [Fact]
    public async Task GetBoardAsync_ShowsAVisitorOnNoRosterOnlyOnADateTheyAttended()
    {
        BoardTestContext context = new();
        Person visitor = await context.AddPersonAsync("Grace", "Hopper");
        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, visitor.Id);

        StandupBoardDto board = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today);
        StandupBoardDto nextDay = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today.AddDays(1));

        Assert.Equal(AttendanceState.Guest, Assert.Single(board.Participants).State);
        Assert.Empty(nextDay.Participants);
    }

    [Fact]
    public async Task AdvanceAsync_PlacesALeaderInLeadersAndGoesNoFurther()
    {
        BoardTestContext context = new();
        Person leader = await context.AddMemberAsync("Grace", "Hopper", RosterRole.Leader);

        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, leader.Id);
        BoardParticipantDto? participant = await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, leader.Id);

        Assert.Equal(AttendanceState.Leader, participant!.State);
        Assert.Equal(BoardTestContext.Now, participant.MarkedAvailableUtc);
        Assert.Equal(AttendanceState.Roster, (await context.Service.RevertAsync(context.Standup.Id, BoardTestContext.Today, leader.Id))!.State);
    }

    [Fact]
    public async Task AdvanceAsync_MarksALeaderAsPresented_WhenTheStandupLetsLeadersPresent()
    {
        BoardTestContext context = new();
        context.Standup.LeadersCanPresent = true;
        Person leader = await context.AddMemberAsync("Grace", "Hopper", RosterRole.Leader);

        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, leader.Id);
        BoardParticipantDto? presented = await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, leader.Id);
        StandupBoardDto board = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today);

        Assert.True(presented!.Presents);
        Assert.Equal(AttendanceState.Presented, presented.State);
        Assert.Equal(BoardTestContext.Now, presented.PresentedUtc);
        Assert.Equal(AttendanceState.Presented, Assert.Single(board.Participants).State);
        Assert.Equal(AttendanceState.Leader, (await context.Service.RevertAsync(context.Standup.Id, BoardTestContext.Today, leader.Id))!.State);
    }

    [Fact]
    public async Task AdvanceAsync_MarksAVisitingGuestAsPresented_WhenTheStandupLetsGuestsPresent()
    {
        BoardTestContext context = new();
        context.Standup.GuestsCanPresent = true;
        Person visitor = await context.AddPersonAsync("Grace", "Hopper");

        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, visitor.Id);
        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, visitor.Id);
        StandupBoardDto board = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today);

        Assert.Equal(AttendanceState.Presented, Assert.Single(board.Participants).State);
    }

    [Fact]
    public async Task GetBoardAsync_HidesALeaderWhoPresented_OnceTheStandupStopsLettingLeadersPresent()
    {
        BoardTestContext context = new();
        context.Standup.LeadersCanPresent = true;
        Person leader = await context.AddMemberAsync("Grace", "Hopper", RosterRole.Leader);
        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, leader.Id);
        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, leader.Id);
        context.Standup.LeadersCanPresent = false;
        await context.Standups.UpsertAsync(context.Standup);

        StandupBoardDto board = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today);

        Assert.Empty(board.Participants);
    }

    [Fact]
    public async Task SaveUpdateAsync_RecordsBlockersAndParkingLotForALeaderWhoCanPresent()
    {
        BoardTestContext context = new();
        context.Standup.LeadersCanPresent = true;
        Person leader = await context.AddMemberAsync("Grace", "Hopper", RosterRole.Leader);
        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, leader.Id);

        BoardParticipantDto? saved = await context.Service.SaveUpdateAsync(new ParticipantUpdateDto
        {
            StandupId = context.Standup.Id,
            PersonId = leader.Id,
            MeetingDate = BoardTestContext.Today,
            Update = "Planning the release.",
            Blockers = "Waiting on access.",
            ParkingLot = "Talk about the release.",
        });

        Assert.Equal("Waiting on access.", saved!.Blockers);
        Assert.Equal("Talk about the release.", saved.ParkingLot);
    }

    [Fact]
    public async Task GetBoardAsync_ReadsAGuestEntryFromBeforeJoiningTheLeaderRosterAsLeading()
    {
        BoardTestContext context = new();
        Person person = await context.AddPersonAsync("Grace", "Hopper");
        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, person.Id);
        await context.AddMemberAsync(person, RosterRole.Leader);

        StandupBoardDto board = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today);

        Assert.Equal(AttendanceState.Leader, Assert.Single(board.Participants).State);
    }

    [Fact]
    public async Task GetBoardAsync_ShowsInactivePeopleOnlyOnADateTheyAttended()
    {
        BoardTestContext context = new();
        Person presented = await context.AddMemberAsync("Ada", "Lovelace");
        Person notHere = await context.AddMemberAsync("Alan", "Turing");
        Person visitor = await context.AddPersonAsync("Grace", "Hopper");
        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, presented.Id);
        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, presented.Id);
        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, visitor.Id);
        foreach (Person person in new[] { presented, notHere, visitor })
        {
            person.IsActive = false;
            await context.People.UpsertAsync(person);
        }

        StandupBoardDto board = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today);

        Assert.Equal(AttendanceState.Presented, Assert.Single(board.Participants, participant => participant.PersonId == presented.Id).State);
        Assert.Equal(AttendanceState.Guest, Assert.Single(board.Participants, participant => participant.PersonId == visitor.Id).State);
        Assert.DoesNotContain(board.Participants, participant => participant.PersonId == notHere.Id);
    }

    [Fact]
    public async Task AdvanceAsync_RefusesAnInactivePersonWhoIsNotAttending()
    {
        BoardTestContext context = new();
        Person presenter = await context.AddMemberAsync("Ada", "Lovelace");
        presenter.IsActive = false;
        await context.People.UpsertAsync(presenter);

        BoardParticipantDto? moved = await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, presenter.Id);

        Assert.Null(moved);
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
    public async Task SaveUpdateAsync_RecordsNotesButNoBlockersOrParkingLotForAGuest()
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
            Blockers = "Waiting on access.",
            ParkingLot = "Talk about the release.",
        });

        Assert.NotNull(saved);
        Assert.Equal(AttendeeKind.Guest, saved.Kind);
        Assert.Equal(AttendanceState.Guest, saved.State);
        Assert.Equal("Just listening in.", saved.Update);
        Assert.Null(saved.Blockers);
        Assert.Null(saved.ParkingLot);
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
    public async Task SetLeaderAsync_AcceptsALeaderAttendingWithoutPresenting()
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
    public async Task LeaderCandidates_LeavesOutPresentersAndGuests_UntilTheDateLetsAnyoneAttendingLead()
    {
        BoardTestContext context = new();
        Person presenter = await context.AddMemberAsync("Ada", "Lovelace");
        Person guest = await context.AddMemberAsync("Grace", "Hopper", RosterRole.Guest);
        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, presenter.Id);
        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, guest.Id);

        StandupBoardDto board = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today);

        Assert.Empty(board.LeaderCandidates);
        Assert.False(await context.Service.SetLeaderAsync(context.Standup.Id, BoardTestContext.Today, presenter.Id));
        Assert.False(await context.Service.SetLeaderAsync(context.Standup.Id, BoardTestContext.Today, guest.Id));
    }

    [Fact]
    public async Task LeaderCandidates_ListsEveryoneAttendingAfterLeaders_WhenTheDateLetsAnyoneAttendingLead()
    {
        BoardTestContext context = new();
        Person presenter = await context.AddMemberAsync("Ada", "Lovelace");
        await context.AddMemberAsync("Alan", "Turing");
        Person guest = await context.AddMemberAsync("Grace", "Hopper", RosterRole.Guest);
        Person visitor = await context.AddPersonAsync("Edsger", "Dijkstra");
        Person leader = await context.AddMemberAsync("Barbara", "Liskov", RosterRole.Leader);
        foreach (Person attending in new[] { presenter, guest, visitor, leader })
        {
            await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, attending.Id);
        }

        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, presenter.Id);
        await context.Service.SetAnyAttendeeCanLeadAsync(context.Standup.Id, BoardTestContext.Today, true);
        StandupBoardDto board = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today);

        Assert.Equal(leader.Id, board.LeaderCandidates[0].PersonId);
        Assert.Equal(new[] { leader.Id, presenter.Id, guest.Id, visitor.Id }.Order(), board.LeaderCandidates.Select(candidate => candidate.PersonId).Order());
        Assert.True(await context.Service.SetLeaderAsync(context.Standup.Id, BoardTestContext.Today, presenter.Id));
        Assert.True(await context.Service.SetLeaderAsync(context.Standup.Id, BoardTestContext.Today, visitor.Id));
        Assert.Empty((await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Yesterday)).LeaderCandidates);
    }

    [Fact]
    public async Task SetAnyAttendeeCanLeadAsync_ClearsALeaderFromOutsideTheLeaderRoster_WhenTurnedOff()
    {
        BoardTestContext context = new();
        Person presenter = await context.AddMemberAsync("Ada", "Lovelace");
        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, presenter.Id);
        await context.Service.SetAnyAttendeeCanLeadAsync(context.Standup.Id, BoardTestContext.Today, true);
        await context.Service.SetLeaderAsync(context.Standup.Id, BoardTestContext.Today, presenter.Id);

        Guid? leaderAfter = await context.Service.SetAnyAttendeeCanLeadAsync(context.Standup.Id, BoardTestContext.Today, false);
        StandupBoardDto board = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today);

        Assert.Null(leaderAfter);
        Assert.Null(board.LeaderPersonId);
        Assert.False(board.AnyAttendeeCanLead);
        Assert.Empty(board.LeaderCandidates);
    }

    [Fact]
    public async Task SetAnyAttendeeCanLeadAsync_KeepsALeaderFromTheLeaderRoster_WhenTurnedOff()
    {
        BoardTestContext context = new();
        Person leader = await context.AddMemberAsync("Grace", "Hopper", RosterRole.Leader);
        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, leader.Id);
        await context.Service.SetAnyAttendeeCanLeadAsync(context.Standup.Id, BoardTestContext.Today, true);
        await context.Service.SetLeaderAsync(context.Standup.Id, BoardTestContext.Today, leader.Id);

        Assert.Equal(leader.Id, await context.Service.SetAnyAttendeeCanLeadAsync(context.Standup.Id, BoardTestContext.Today, false));
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
