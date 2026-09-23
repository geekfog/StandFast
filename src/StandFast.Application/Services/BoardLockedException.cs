using StandFast.Domain.Common;

namespace StandFast.Application.Services;

/// <summary>
/// Thrown when a change is attempted against a date whose board has been locked. The board disables its controls once it knows about a lock, so this
/// is what a screen opened before someone else locked the date runs into.
/// </summary>
public sealed class BoardLockedException(Guid standupId, DateOnly meetingDate, DateTimeOffset lockedUtc)
    : InvalidOperationException($"The board for {MeetingCalendar.ToRouteValue(meetingDate)} was locked at {lockedUtc:u}.")
{
    public Guid StandupId { get; } = standupId;

    public DateOnly MeetingDate { get; } = meetingDate;

    public DateTimeOffset LockedUtc { get; } = lockedUtc;
}
