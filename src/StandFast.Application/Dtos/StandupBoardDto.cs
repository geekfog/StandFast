using StandFast.Domain.Common;

namespace StandFast.Application.Dtos;

/// <summary>The whole board for one standup on one date. The UI splits <see cref="Participants"/> into columns by attendance state.</summary>
/// <param name="Participants">Everyone on the presenter, leader, and guest rosters, each under the first of those they are on, plus anyone else who visited on this date.</param>
/// <param name="Leaders">Everyone on the standup's leader roster, in roster order. The leader picker offers those of them attending; see <see cref="LeaderCandidates"/>.</param>
/// <param name="AnyAttendeeCanLead">True when this date lets anyone attending be picked to lead, not only those on the leader roster.</param>
/// <param name="LeaderPersonId">Whoever is leading this date, or null while nobody is picked. Leading is optional, so null is a normal state.</param>
/// <param name="LockedUtc">When this date was closed to changes, or null while it is open. The board reads its whole locked state from this one value.</param>
/// <param name="PriorPresentationOrder">
/// Turn each participant took at the most recent earlier meeting, keyed by person id, starting at one. A participant missing from the map did not
/// present then. This is a property of the board rather than of a participant, so replacing one participant after a tap cannot make it go stale.
/// </param>
/// <param name="Sprint">The standup's sprint covering this date, or null when none does.</param>
/// <param name="Availability">Days off bearing on this date or the rest of its sprint, keyed by person id. Anyone missing is available throughout.</param>
public sealed record StandupBoardDto(
    Guid StandupId,
    string StandupName,
    DateOnly MeetingDate,
    bool IsScheduledDay,
    IReadOnlyList<BoardParticipantDto> Participants,
    IReadOnlyList<StandupMemberDto> Leaders,
    bool AnyAttendeeCanLead,
    Guid? LeaderPersonId,
    DateTimeOffset? LockedUtc,
    IReadOnlyDictionary<Guid, int> PriorPresentationOrder,
    SprintStatusDto? Sprint,
    IReadOnlyDictionary<Guid, PersonAvailabilityDto> Availability)
{
    public static StandupBoardDto Empty(DateOnly meetingDate) =>
        new(Guid.Empty, string.Empty, meetingDate, false, [], [], false, null, null, new Dictionary<Guid, int>(), null, new Dictionary<Guid, PersonAvailabilityDto>());

    /// <summary>The participant's days off as they bear on this board, or null when they have none that do.</summary>
    public PersonAvailabilityDto? AvailabilityFor(Guid personId) => Availability.GetValueOrDefault(personId);

    public bool IsLocked => LockedUtc is not null;

    /// <summary>
    /// Who the leader picker offers: the leader roster members attending this date, then, where <see cref="AnyAttendeeCanLead"/> is on, everyone else
    /// attending. Whoever is already recorded as leading stays in the list, so a date recorded before leaders had to attend still shows its leader.
    /// </summary>
    public IReadOnlyList<StandupMemberDto> LeaderCandidates =>
    [
        .. Leaders.Where(leader => leader.PersonId == LeaderPersonId
            || Participants.Any(participant => participant.PersonId == leader.PersonId && AttendanceTransitions.IsAttending(participant.State))),
        .. Participants
            .Where(participant => Leaders.All(leader => leader.PersonId != participant.PersonId)
                && (participant.PersonId == LeaderPersonId || (AnyAttendeeCanLead && AttendanceTransitions.IsAttending(participant.State))))
            .Select(participant => new StandupMemberDto(StandupId, participant.PersonId, participant.DisplayName, participant.Email, participant.DisplayOrder, IsActive: true)),
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
