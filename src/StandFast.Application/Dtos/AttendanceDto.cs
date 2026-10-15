namespace StandFast.Application.Dtos;

/// <summary>
/// The attendance report for one standup over one date range: one row per person on the presenter roster or who presented in the period,
/// measured against the days the standup actually ran.
/// </summary>
/// <param name="MeetingCount">Days in the period on which at least one person presented, which is what a person's attendance is measured against.</param>
public sealed record AttendanceDto(
    Guid StandupId,
    string StandupName,
    DateOnly From,
    DateOnly To,
    int MeetingCount,
    IReadOnlyList<PersonAttendanceDto> People) : IReportHeader
{
    public static AttendanceDto Empty(Guid standupId, string standupName, DateOnly from, DateOnly to) => new(standupId, standupName, from, to, 0, []);

    public bool HasData => MeetingCount > 0 && People.Count > 0;
}

/// <summary>One person's attendance and turn lengths across the report's period.</summary>
/// <param name="Presented">Days in the period they took a turn.</param>
/// <param name="Missed">
/// Days the standup ran, on or after they joined the roster, that they took no turn. Null for someone no longer on the roster, because a
/// removal keeps no date and the days they were expected cannot be known.
/// </param>
/// <param name="TurnLengths">Every turn of theirs with a known end: up to the next person's turn, or for the day's last turn, up to the lock.</param>
public sealed record PersonAttendanceDto(Guid PersonId, string DisplayName, int Presented, int? Missed, IReadOnlyList<TimeSpan> TurnLengths)
{
    /// <summary>Share of the days they were expected that they presented on, from 0 to 1. Null when <see cref="Missed"/> is unknown.</summary>
    public double? AttendanceRate => Missed is { } missed && Presented + missed > 0 ? (double)Presented / (Presented + missed) : null;

    public TimeSpan? AverageTurn => TurnLengths.Count == 0 ? null : TimeSpan.FromTicks((long)TurnLengths.Average(length => length.Ticks));

    public TimeSpan? ShortestTurn => TurnLengths.Count == 0 ? null : TurnLengths.Min();

    public TimeSpan? LongestTurn => TurnLengths.Count == 0 ? null : TurnLengths.Max();
}
