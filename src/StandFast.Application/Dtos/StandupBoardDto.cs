namespace StandFast.Application.Dtos;

/// <summary>The whole board for one standup on one date. The UI splits <see cref="Participants"/> into columns by attendance state.</summary>
public sealed record StandupBoardDto(Guid StandupId, string StandupName, DateOnly MeetingDate, bool IsScheduledDay, IReadOnlyList<BoardParticipantDto> Participants)
{
    public static StandupBoardDto Empty(DateOnly meetingDate) => new(Guid.Empty, string.Empty, meetingDate, false, []);
}
