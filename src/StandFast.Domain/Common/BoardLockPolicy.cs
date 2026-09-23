namespace StandFast.Domain.Common;

/// <summary>
/// What time a lock on a day's board is recorded as having happened. The board is usually locked while everyone is still in the call, and then the
/// moment it was locked is the truth. Locked later it is dated from the day's last turn instead, so a board closed the next morning does not carry a
/// closing time hours after anybody was there.
/// </summary>
public static class BoardLockPolicy
{
    /// <param name="lastTurnUtc">When the last person presented that day, or null when nobody did, which leaves the current time as the only fact about the lock.</param>
    /// <param name="graceAfterLastTurn">How long after that turn the lock still records the current time.</param>
    /// <param name="tailAfterLastTurn">Added to the last turn to date a lock applied beyond the grace window.</param>
    public static DateTimeOffset LockedAt(DateTimeOffset now, DateTimeOffset? lastTurnUtc, TimeSpan graceAfterLastTurn, TimeSpan tailAfterLastTurn) =>
        lastTurnUtc is { } lastTurn && now - lastTurn > graceAfterLastTurn ? lastTurn + tailAfterLastTurn : now;
}
