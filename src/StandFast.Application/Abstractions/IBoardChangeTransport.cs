using StandFast.Application.Notifications;

namespace StandFast.Application.Abstractions;

/// <summary>Carries board changes between server copies. Each copy hears the changes the others send, never its own.</summary>
public interface IBoardChangeTransport
{
    /// <summary>Raised for each change another server copy sent.</summary>
    event Action<BoardChange>? Received;

    Task SendAsync(BoardChange change, CancellationToken cancellationToken = default);
}
