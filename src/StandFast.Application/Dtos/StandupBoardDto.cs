namespace StandFast.Application.Dtos;

/// <summary>The whole board for one standup on one date. The UI splits <see cref="Participants"/> into columns by attendance state.</summary>
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
    IReadOnlyDictionary<Guid, int> PriorPresentationOrder)
{
    public static StandupBoardDto Empty(DateOnly meetingDate) => new(Guid.Empty, string.Empty, meetingDate, false, [], new Dictionary<Guid, int>());

    /// <summary>The participant's turn at the previous meeting, or null when they were not there.</summary>
    public int? PriorTurnFor(Guid personId) => PriorPresentationOrder.TryGetValue(personId, out int order) ? order : null;
}
