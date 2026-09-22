namespace StandFast.Application.Dtos;

/// <summary>
/// The presenting order report for one standup over one date range: a shared list of meeting dates, and one series per person who presented
/// at least once. Every series is aligned to <see cref="MeetingDates"/>, so the chart and the table both read a series by index.
/// </summary>
/// <param name="MeetingDates">Ascending dates on which at least one person presented. Days the standup produced nothing are left out rather than drawn as empty columns.</param>
/// <param name="HighestOrder">The largest turn number anywhere in the report, which sizes the chart's order axis.</param>
public sealed record PresentingOrderTimelineDto(
    Guid StandupId,
    string StandupName,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<DateOnly> MeetingDates,
    IReadOnlyList<PresenterTimelineDto> Presenters,
    int HighestOrder)
{
    public static PresentingOrderTimelineDto Empty(Guid standupId, string standupName, DateOnly from, DateOnly to) =>
        new(standupId, standupName, from, to, [], [], 0);

    public bool HasData => MeetingDates.Count > 0 && Presenters.Count > 0;
}

/// <summary>
/// One person's line on the report. <paramref name="OrdersByDate"/> has an entry for every meeting date in the report, holding the turn
/// they took or null on a date they did not present.
/// </summary>
public sealed record PresenterTimelineDto(Guid PersonId, string DisplayName, IReadOnlyList<int?> OrdersByDate);
