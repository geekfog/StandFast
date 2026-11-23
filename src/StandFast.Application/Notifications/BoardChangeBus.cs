using Microsoft.Extensions.Logging;
using StandFast.Application.Abstractions;

namespace StandFast.Application.Notifications;

/// <summary>
/// One per server copy. Hands each board change to the boards open on this copy for that standup and date, and sends it through the transport so the other
/// copies do the same. Boards here hear a change at once, without waiting for the transport.
/// </summary>
public sealed class BoardChangeBus : IDisposable
{
    private readonly IBoardChangeTransport transport;
    private readonly ILogger<BoardChangeBus> logger;
    private readonly Lock gate = new();
    private readonly Dictionary<(Guid StandupId, DateOnly MeetingDate), List<Subscription>> subscriptions = [];

    public BoardChangeBus(IBoardChangeTransport transport, ILogger<BoardChangeBus> logger)
    {
        this.transport = transport;
        this.logger = logger;
        transport.Received += Deliver;
    }

    public void Publish(BoardChange change)
    {
        Deliver(change);
        _ = SendAsync(change);
    }

    public IDisposable Subscribe(Guid standupId, DateOnly meetingDate, Guid originId, Func<Task> onChanged)
    {
        Subscription subscription = new(this, (standupId, meetingDate), originId, onChanged);

        lock (gate)
        {
            if (!subscriptions.TryGetValue(subscription.Key, out List<Subscription>? list))
            {
                subscriptions[subscription.Key] = list = [];
            }

            list.Add(subscription);
        }

        return subscription;
    }

    public void Dispose() => transport.Received -= Deliver;

    private void Deliver(BoardChange change)
    {
        Subscription[] targets;

        lock (gate)
        {
            targets = subscriptions.TryGetValue((change.StandupId, change.MeetingDate), out List<Subscription>? list)
                ? [.. list.Where(subscription => subscription.OriginId != change.OriginId)]
                : [];
        }

        foreach (Subscription target in targets)
        {
            _ = NotifyAsync(target, change);
        }
    }

    private async Task SendAsync(BoardChange change)
    {
        try
        {
            await transport.SendAsync(change);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Boards on other server copies were not told about the change to standup {StandupId} on {MeetingDate}.", change.StandupId, change.MeetingDate);
        }
    }

    private async Task NotifyAsync(Subscription target, BoardChange change)
    {
        try
        {
            await target.OnChanged();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "An open board failed to reload after the change to standup {StandupId} on {MeetingDate}.", change.StandupId, change.MeetingDate);
        }
    }

    private void Remove(Subscription subscription)
    {
        lock (gate)
        {
            if (subscriptions.TryGetValue(subscription.Key, out List<Subscription>? list) && list.Remove(subscription) && list.Count == 0)
            {
                subscriptions.Remove(subscription.Key);
            }
        }
    }

    private sealed class Subscription(BoardChangeBus owner, (Guid StandupId, DateOnly MeetingDate) key, Guid originId, Func<Task> onChanged) : IDisposable
    {
        public (Guid StandupId, DateOnly MeetingDate) Key { get; } = key;

        public Guid OriginId { get; } = originId;

        public Func<Task> OnChanged { get; } = onChanged;

        public void Dispose() => owner.Remove(this);
    }
}
