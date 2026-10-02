using StandFast.Domain.Common;

namespace StandFast.Application.Dtos;

/// <summary>The whole board for one standup on one date. The UI splits <see cref="Participants"/> into columns by attendance state.</summary>
/// <param name="Participants">Everyone on the presenter roster, and every other active person in the directory as a possible guest.</param>
/// <param name="Leaders">Everyone on the standup's leader roster, in roster order. The leader picker offers those of them attending; see <see cref="LeaderCandidates"/>.</param>
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

    /// <summary>
    /// Who the leader picker offers: the leader roster members attending this date, as a guest or as a presenter. Whoever is already recorded as leading
    /// stays in the list, so a date recorded before leaders had to attend still shows its leader.
    /// </summary>
    public IReadOnlyList<StandupMemberDto> LeaderCandidates =>
    [
        .. Leaders.Where(leader => leader.PersonId == LeaderPersonId
            || Participants.Any(participant => participant.PersonId == leader.PersonId && AttendanceTransitions.IsAttending(participant.State))),
    ];

    /// <summary>The participant's turn at the previous meeting, or null when they were not there.</summary>
    public int? PriorTurnFor(Guid personId) => PriorPresentationOrder.TryGetValue(personId, out int order) ? order : null;

    /// <summary>
    /// The board with one participant replaced after a change to them, rather than refetched. A leader who stops attending stops leading, which is what
    /// the board service records alongside the same change.
    /// </summary>
    public StandupBoardDto WithParticipant(BoardParticipantDto updated) => this with
    {
        Participants = [.. Participants.Select(participant => participant.PersonId == updated.PersonId ? updated : participant)],
        LeaderPersonId = LeaderPersonId == updated.PersonId && !AttendanceTransitions.IsAttending(updated.State) ? null : LeaderPersonId,
    };
}
