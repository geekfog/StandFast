namespace StandFast.Domain.Entities;

/// <summary>The entry for the requested date plus the most recent entry before it. Both come back from a single storage range query.</summary>
/// <param name="Current">The entry for the requested meeting date, or null if nothing has been recorded yet.</param>
/// <param name="Prior">The most recent entry on an earlier date, or null if this is the participant's first entry in the standup.</param>
public sealed record StandupEntryPair(StandupEntry? Current, StandupEntry? Prior)
{
    public static readonly StandupEntryPair Empty = new(null, null);
}
