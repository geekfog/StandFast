namespace StandFast.Application.Abstractions;

/// <summary>
/// Keeps every open board for a standup and date in step when one of them saves a change, on whichever server copy each is open. Scoped to one circuit,
/// which never hears about its own changes, since it already shows them.
/// </summary>
public interface IBoardChangeNotifier
{
    /// <summary>Tells the other open boards for the standup and date to reload. It returns at once; reaching other server copies happens in the background and a failure is logged.</summary>
    void Publish(Guid standupId, DateOnly meetingDate);

    /// <summary>Calls <paramref name="onChanged"/> whenever another circuit saves a change to the board for the standup and date, until the result is disposed.</summary>
    IDisposable Subscribe(Guid standupId, DateOnly meetingDate, Func<Task> onChanged);
}
