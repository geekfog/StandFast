namespace StandFast.Domain.Common;

/// <summary>
/// The one definition of "what order did people go in". The board reads it for the previous meeting so a late speaker can be called early
/// today, and the presenting order report reads it for every meeting in a date range.
/// </summary>
public static class PresentationOrder
{
    /// <summary>Turn numbers start here, so the first speaker of a meeting is the 1st rather than the 0th.</summary>
    public const int FirstTurn = 1;

    /// <summary>Stands in for a turn number wherever someone did not present, so they read as sorting after everyone who did.</summary>
    public const string NoTurnSymbol = "∞";

    /// <summary>
    /// Ranks one meeting's turns by the time each participant presented. Callers pass a single meeting's presentations: ranking across dates
    /// would order by date rather than by turn.
    /// </summary>
    public static IReadOnlyDictionary<Guid, int> WithinMeeting(IEnumerable<Presentation> presentations) => presentations
        .OrderBy(presentation => presentation.PresentedUtc)
        .Select((presentation, index) => (presentation.PersonId, Order: index + FirstTurn))
        .ToDictionary(ranked => ranked.PersonId, ranked => ranked.Order);

    /// <summary>
    /// Ranks the turns taken at the latest meeting present in <paramref name="presentations"/>. Each participant's prior entry can come from a
    /// different date, so the latest date across the whole set is the meeting everyone is ranked against. Anyone who did not present at that
    /// meeting is absent from the result, which the board renders as "no previous turn".
    /// </summary>
    public static IReadOnlyDictionary<Guid, int> AtMostRecentMeeting(IEnumerable<Presentation> presentations)
    {
        List<Presentation> candidates = [.. presentations];
        if (candidates.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        DateOnly mostRecentMeeting = candidates.Max(presentation => presentation.MeetingDate);
        return WithinMeeting(candidates.Where(presentation => presentation.MeetingDate == mostRecentMeeting));
    }
}
