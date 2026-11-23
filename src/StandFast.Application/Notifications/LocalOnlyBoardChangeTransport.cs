using StandFast.Application.Abstractions;

namespace StandFast.Application.Notifications;

/// <summary>The transport when no cross-server channel is configured: boards on this server copy still stay in step, and nothing leaves it.</summary>
public sealed class LocalOnlyBoardChangeTransport : IBoardChangeTransport
{
    public event Action<BoardChange>? Received
    {
        add { }
        remove { }
    }

    public Task SendAsync(BoardChange change, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
