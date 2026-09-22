namespace StandFast.Domain.Common;

/// <summary>
/// One completed turn: a participant gave their update at a meeting, and the moment they did. It is the slice of a standup entry that
/// ordering and reporting work from, so neither has to carry an entry's attendance state or update text around.
/// </summary>
public readonly record struct Presentation(Guid PersonId, DateOnly MeetingDate, DateTimeOffset PresentedUtc);
