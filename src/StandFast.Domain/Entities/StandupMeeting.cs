namespace StandFast.Domain.Entities;

/// <summary>
/// What is recorded about one standup on one meeting date apart from any participant. A meeting row exists only once something has been set on it,
/// so a date with no row is a meeting nobody has annotated rather than a meeting that did not happen.
/// </summary>
public sealed class StandupMeeting
{
    public Guid StandupId { get; set; }

    public DateOnly MeetingDate { get; set; }

    /// <summary>The person running the standup that day, or null when nobody is picked. Leading is optional, so no leader is a normal state.</summary>
    public Guid? LeaderPersonId { get; set; }

    /// <summary>
    /// The moment this date's board was closed to changes, or null while it is open. The timestamp carries the flag on its own, so there is no
    /// second boolean that can disagree with it, and when a standup was closed stays on the record.
    /// </summary>
    public DateTimeOffset? LockedUtc { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }

    public string? ETag { get; set; }

    public bool IsLocked => LockedUtc is not null;
}
