using StandFast.Domain.Entities;

namespace StandFast.Domain.Common;

/// <summary>
/// Works out the order people presented in at the standup's most recent earlier meeting, so the board can show who went late last time
/// and should be called earlier today.
/// </summary>
public static class PriorPresentationOrder
{
    /// <summary>
    /// Ranks by the time each participant presented, starting at one. Only the single most recent earlier meeting counts: each participant's
    /// prior entry can come from a different date, and ranking across dates would order by date rather than by turn. Anyone who did not present
    /// at that meeting is absent from the result, which the board renders as "no previous turn".
    /// </summary>
    public static IReadOnlyDictionary<Guid, int> Rank(IEnumerable<StandupEntry?> priorEntries)
    {
        List<StandupEntry> presented = [.. priorEntries.OfType<StandupEntry>().Where(entry => entry.PresentedUtc is not null)];
        if (presented.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        DateOnly mostRecentMeeting = presented.Max(entry => entry.MeetingDate);

        return presented
            .Where(entry => entry.MeetingDate == mostRecentMeeting)
            .OrderBy(entry => entry.PresentedUtc)
            .Select((entry, index) => (entry.PersonId, Order: index + 1))
            .ToDictionary(ranked => ranked.PersonId, ranked => ranked.Order);
    }
}
