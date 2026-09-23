namespace StandFast.Application.Dtos;

/// <summary>The whole board for one standup on one date. The UI splits <see cref="Participants"/> into columns by attendance state.</summary>
/// <param name="Leaders">Everyone on the standup's leader roster, in roster order, which is what the leader picker offers for this date.</param>
/// <param name="LeaderPersonId">Whoever is leading this date, or null while nobody is picked. Leading is optional, so null is a normal state.</param>
/// <param name="LockedUtc">When this date was closed to changes, or null while it is open. The board reads its whole locked state from this one value.</param>
/// <param name="PriorPresentationOrder">
/// Turn each participant took at the most recent earlier meeting, keyed by person id, starting at one. A participant missing from the map did not
/// present then. This is a property of the board rather than of a participant, so replacing one participant after a tap cannot make it go stale.
/// </param>
public sealed record StandupBoardDto(
    Guid StandupId,
    string StandupName,
    DateOnly MeetingDate,
    bool IsScheduledDay,
    IReadOnlyList<BoardParticipantDto> Participants,
    IReadOnlyList<StandupMemberDto> Leaders,
    Guid? LeaderPersonId,
    DateTimeOffset? LockedUtc,
    IReadOnlyDictionary<Guid, int> PriorPresentationOrder)
{
    public static StandupBoardDto Empty(DateOnly meetingDate) => new(Guid.Empty, string.Empty, meetingDate, false, [], [], null, null, new Dictionary<Guid, int>());

    public bool IsLocked => LockedUtc is not null;

    /// <summary>The participant's turn at the previous meeting, or null when they were not there.</summary>
    public int? PriorTurnFor(Guid personId) => PriorPresentationOrder.TryGetValue(personId, out int order) ? order : null;
}
