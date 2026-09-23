using StandFast.Application.Dtos;
using StandFast.Application.Services;
using StandFast.Application.Tests.Fakes;
using StandFast.Domain.Entities;
using StandFast.Domain.Enums;

namespace StandFast.Application.Tests.Services;

public sealed class BoardLockTests
{
    [Fact]
    public async Task LockAsync_RecordsTheCurrentTimeWhileTheStandupIsStillFresh()
    {
        BoardTestContext context = new();
        Person person = await context.AddMemberAsync("Ada", "Lovelace");
        await PresentAsync(context, person);

        context.Clock.UtcNow = BoardTestContext.Now.AddMinutes(10);
        DateTimeOffset? lockedUtc = await context.Service.LockAsync(context.Standup.Id, BoardTestContext.Today);

        Assert.Equal(context.Clock.UtcNow, lockedUtc);
    }

    [Fact]
    public async Task LockAsync_DatesALateLockFromTheLastTurnRatherThanFromNow()
    {
        BoardTestContext context = new();
        Person person = await context.AddMemberAsync("Ada", "Lovelace");
        DateTimeOffset lastTurn = await PresentAsync(context, person);

        context.Clock.UtcNow = BoardTestContext.Now.AddDays(1);
        DateTimeOffset? lockedUtc = await context.Service.LockAsync(context.Standup.Id, BoardTestContext.Today);

        Assert.Equal(lastTurn.AddMinutes(context.LockOptions.MinutesAfterLastTurn), lockedUtc);
    }

    [Fact]
    public async Task LockAsync_RecordsTheCurrentTimeWhenNobodyPresented()
    {
        BoardTestContext context = new();
        await context.AddMemberAsync("Ada", "Lovelace");

        context.Clock.UtcNow = BoardTestContext.Now.AddDays(1);
        DateTimeOffset? lockedUtc = await context.Service.LockAsync(context.Standup.Id, BoardTestContext.Today);

        Assert.Equal(context.Clock.UtcNow, lockedUtc);
    }

    [Fact]
    public async Task LockAsync_KeepsTheOriginalMomentWhenTheDateIsAlreadyLocked()
    {
        BoardTestContext context = new();
        await context.AddMemberAsync("Ada", "Lovelace");

        DateTimeOffset? first = await context.Service.LockAsync(context.Standup.Id, BoardTestContext.Today);
        context.Clock.UtcNow = BoardTestContext.Now.AddHours(3);
        DateTimeOffset? second = await context.Service.LockAsync(context.Standup.Id, BoardTestContext.Today);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task GetBoardAsync_ReportsTheDateAsLocked()
    {
        BoardTestContext context = new();
        await context.AddMemberAsync("Ada", "Lovelace");
        DateTimeOffset? lockedUtc = await context.Service.LockAsync(context.Standup.Id, BoardTestContext.Today);

        StandupBoardDto board = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today);

        Assert.True(board.IsLocked);
        Assert.Equal(lockedUtc, board.LockedUtc);
    }

    [Fact]
    public async Task AdvanceAsync_IsRefusedOnALockedDate()
    {
        BoardTestContext context = new();
        Person person = await context.AddMemberAsync("Ada", "Lovelace");
        await context.Service.LockAsync(context.Standup.Id, BoardTestContext.Today);

        await Assert.ThrowsAsync<BoardLockedException>(() => context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, person.Id));
    }

    [Fact]
    public async Task SaveUpdateAsync_IsRefusedOnALockedDate()
    {
        BoardTestContext context = new();
        Person person = await context.AddMemberAsync("Ada", "Lovelace");
        await context.Service.LockAsync(context.Standup.Id, BoardTestContext.Today);

        ParticipantUpdateDto update = new()
        {
            StandupId = context.Standup.Id,
            PersonId = person.Id,
            MeetingDate = BoardTestContext.Today,
            Update = "Anything at all.",
        };

        await Assert.ThrowsAsync<BoardLockedException>(() => context.Service.SaveUpdateAsync(update));
    }

    [Fact]
    public async Task SetLeaderAsync_IsRefusedOnALockedDate()
    {
        BoardTestContext context = new();
        Person person = await context.AddMemberAsync("Ada", "Lovelace", RosterRole.Leader);
        await context.Service.LockAsync(context.Standup.Id, BoardTestContext.Today);

        await Assert.ThrowsAsync<BoardLockedException>(() => context.Service.SetLeaderAsync(context.Standup.Id, BoardTestContext.Today, person.Id));
    }

    [Fact]
    public async Task GetLockedDatesAsync_ReturnsOnlyTheLockedDatesInTheRange()
    {
        BoardTestContext context = new();
        await context.AddMemberAsync("Ada", "Lovelace");

        await context.Service.LockAsync(context.Standup.Id, BoardTestContext.Yesterday);
        await context.Service.SetLeaderAsync(context.Standup.Id, BoardTestContext.Today, null);

        IReadOnlyCollection<DateOnly> locked = await context.Service.GetLockedDatesAsync(
            context.Standup.Id, BoardTestContext.Today.AddDays(-7), BoardTestContext.Today);

        Assert.Equal([BoardTestContext.Yesterday], locked);
    }

    [Fact]
    public async Task UnlockAsync_ReopensTheDateForChanges()
    {
        BoardTestContext context = new();
        Person person = await context.AddMemberAsync("Ada", "Lovelace");
        await context.Service.LockAsync(context.Standup.Id, BoardTestContext.Today);

        await context.Service.UnlockAsync(context.Standup.Id, BoardTestContext.Today);
        BoardParticipantDto? participant = await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, person.Id);
        StandupBoardDto board = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today);

        Assert.False(board.IsLocked);
        Assert.NotNull(participant);
    }

    [Fact]
    public async Task LockAsync_LeavesTheLeaderAlreadyRecordedForTheDate()
    {
        BoardTestContext context = new();
        Person leader = await context.AddMemberAsync("Grace", "Hopper", RosterRole.Leader);
        await context.Service.SetLeaderAsync(context.Standup.Id, BoardTestContext.Today, leader.Id);

        await context.Service.LockAsync(context.Standup.Id, BoardTestContext.Today);
        StandupBoardDto board = await context.Service.GetBoardAsync(context.Standup.Id, BoardTestContext.Today);

        Assert.Equal(leader.Id, board.LeaderPersonId);
        Assert.True(board.IsLocked);
    }

    /// <summary>Taps a participant all the way to presented and answers with the moment their turn was recorded.</summary>
    private static async Task<DateTimeOffset> PresentAsync(BoardTestContext context, Person person)
    {
        await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, person.Id);
        BoardParticipantDto? participant = await context.Service.AdvanceAsync(context.Standup.Id, BoardTestContext.Today, person.Id);

        return participant!.PresentedUtc!.Value;
    }
}
