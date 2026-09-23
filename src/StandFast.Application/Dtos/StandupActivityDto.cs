namespace StandFast.Application.Dtos;

/// <summary>
/// The activity report for one standup over one date range: one row per date somebody presented on, newest first. Dates on which nothing was
/// recorded are left out, because the report answers "when did this standup actually run" rather than "when was it scheduled".
/// </summary>
public sealed record StandupActivityDto(
    Guid StandupId,
    string StandupName,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<StandupActivityDayDto> Days) : IReportHeader
{
    public static StandupActivityDto Empty(Guid standupId, string standupName, DateOnly from, DateOnly to) => new(standupId, standupName, from, to, []);

    public bool HasData => Days.Count > 0;
}

/// <summary>
/// One standup as it ran on one date.
/// </summary>
/// <param name="LockedUtc">When the date was closed to changes, or null while it is still open.</param>
/// <param name="PresenterCount">How many people gave their update that day.</param>
/// <param name="FirstPresentedUtc">The first turn of the day, which is where the standup is taken to have started.</param>
public sealed record StandupActivityDayDto(DateOnly MeetingDate, DateTimeOffset? LockedUtc, int PresenterCount, DateTimeOffset FirstPresentedUtc)
{
    public bool IsLocked => LockedUtc is not null;

    /// <summary>
    /// How long the standup took, from its first turn to the moment it was locked. Null while the date is still open, since an unlocked standup
    /// has no end: the lock is what says the meeting finished.
    /// </summary>
    public int? DurationMinutes => LockedUtc is { } lockedUtc ? (int)Math.Round((lockedUtc - FirstPresentedUtc).TotalMinutes) : null;
}
