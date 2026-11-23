using StandFast.Application.Abstractions;

namespace StandFast.Application.Notifications;

/// <summary>One circuit's view of the <see cref="BoardChangeBus"/>, stamping what it publishes with an origin its own subscriptions ignore.</summary>
public sealed class BoardChangeNotifier(BoardChangeBus bus) : IBoardChangeNotifier
{
    private readonly Guid originId = Guid.NewGuid();

    public void Publish(Guid standupId, DateOnly meetingDate) => bus.Publish(new BoardChange(standupId, meetingDate, originId));

    public IDisposable Subscribe(Guid standupId, DateOnly meetingDate, Func<Task> onChanged) => bus.Subscribe(standupId, meetingDate, originId, onChanged);
}
