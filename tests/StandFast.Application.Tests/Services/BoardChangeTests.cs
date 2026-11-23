using Microsoft.Extensions.Logging.Abstractions;
using StandFast.Application.Abstractions;
using StandFast.Application.Dtos;
using StandFast.Application.Notifications;
using StandFast.Application.Tests.Fakes;
using StandFast.Domain.Entities;

namespace StandFast.Application.Tests.Services;

public sealed class BoardChangeTests
{
    private static readonly Guid StandupId = Guid.NewGuid();
    private static readonly DateOnly Today = BoardTestContext.Today;

    [Fact]
    public async Task BoardService_TellsOtherBoardsAboutEverySavedChange()
    {
        BoardTestContext context = new();
        Person person = await context.AddMemberAsync("Ada", "Lovelace");
        Guid standupId = context.Standup.Id;

        await context.Service.AdvanceAsync(standupId, Today, person.Id);
        await context.Service.SaveUpdateAsync(new ParticipantUpdateDto { StandupId = standupId, PersonId = person.Id, MeetingDate = Today, Update = "Done the migration." });
        await context.Service.SetLeaderAsync(standupId, Today, null);
        await context.Service.SetAnyAttendeeCanLeadAsync(standupId, Today, anyAttendeeCanLead: true);
        await context.Service.LockAsync(standupId, Today);
        await context.Service.UnlockAsync(standupId, Today);

        Assert.Equal(6, context.Changes.Published.Count);
        Assert.All(context.Changes.Published, change => Assert.Equal((standupId, Today), change));
    }

    [Fact]
    public async Task BoardService_StaysQuietWhenNothingChanges()
    {
        BoardTestContext context = new();
        await context.AddMemberAsync("Ada", "Lovelace");
        Guid standupId = context.Standup.Id;

        await context.Service.UnlockAsync(standupId, Today);
        await context.Service.SetAnyAttendeeCanLeadAsync(standupId, Today, anyAttendeeCanLead: false);

        Assert.Empty(context.Changes.Published);
    }

    [Fact]
    public void Bus_ReachesOtherCircuitsOnTheSameBoardButNotTheOneThatSaved()
    {
        BoardChangeBus bus = NewBus(new FakeTransport());
        BoardChangeNotifier saver = new(bus);
        BoardChangeNotifier viewer = new(bus);
        int saverCalls = 0, viewerCalls = 0;
        using IDisposable saverSubscription = saver.Subscribe(StandupId, Today, () => Count(ref saverCalls));
        using IDisposable viewerSubscription = viewer.Subscribe(StandupId, Today, () => Count(ref viewerCalls));

        saver.Publish(StandupId, Today);

        Assert.Equal(0, saverCalls);
        Assert.Equal(1, viewerCalls);
    }

    [Fact]
    public void Bus_LeavesOtherDatesAndDisposedSubscriptionsAlone()
    {
        BoardChangeBus bus = NewBus(new FakeTransport());
        BoardChangeNotifier viewer = new(bus);
        int otherDateCalls = 0, disposedCalls = 0;
        using IDisposable otherDate = viewer.Subscribe(StandupId, Today.AddDays(1), () => Count(ref otherDateCalls));
        viewer.Subscribe(StandupId, Today, () => Count(ref disposedCalls)).Dispose();

        new BoardChangeNotifier(bus).Publish(StandupId, Today);

        Assert.Equal(0, otherDateCalls);
        Assert.Equal(0, disposedCalls);
    }

    [Fact]
    public void Bus_SendsLocalChangesOnAndDeliversTheOnesOtherServerCopiesSend()
    {
        FakeTransport transport = new();
        BoardChangeBus bus = NewBus(transport);
        int calls = 0;
        using IDisposable subscription = new BoardChangeNotifier(bus).Subscribe(StandupId, Today, () => Count(ref calls));

        new BoardChangeNotifier(bus).Publish(StandupId, Today);
        transport.Receive(new BoardChange(StandupId, Today, Guid.NewGuid()));

        Assert.Single(transport.Sent);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Bus_StillReachesBoardsOnThisCopyWhenTheTransportFails()
    {
        BoardChangeBus bus = NewBus(new FakeTransport { Fails = true });
        int calls = 0;
        using IDisposable subscription = new BoardChangeNotifier(bus).Subscribe(StandupId, Today, () => Count(ref calls));

        new BoardChangeNotifier(bus).Publish(StandupId, Today);

        Assert.Equal(1, calls);
    }

    private static BoardChangeBus NewBus(IBoardChangeTransport transport) => new(transport, NullLogger<BoardChangeBus>.Instance);

    private static Task Count(ref int calls)
    {
        calls++;
        return Task.CompletedTask;
    }

    private sealed class FakeTransport : IBoardChangeTransport
    {
        public event Action<BoardChange>? Received;

        public bool Fails { get; init; }

        public List<BoardChange> Sent { get; } = [];

        public Task SendAsync(BoardChange change, CancellationToken cancellationToken = default)
        {
            if (Fails)
            {
                throw new InvalidOperationException("The transport is down.");
            }

            Sent.Add(change);
            return Task.CompletedTask;
        }

        public void Receive(BoardChange change) => Received?.Invoke(change);
    }
}
