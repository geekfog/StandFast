using StandFast.Application.Dtos;

namespace StandFast.Application.Services;

public interface IBoardService
{
    Task<StandupBoardDto> GetBoardAsync(Guid standupId, DateOnly meetingDate, CancellationToken cancellationToken = default);

    /// <summary>Tap forward: roster to available, available to presented.</summary>
    Task<BoardParticipantDto?> AdvanceAsync(Guid standupId, DateOnly meetingDate, Guid personId, CancellationToken cancellationToken = default);

    /// <summary>Undo a tap by moving the participant one column back.</summary>
    Task<BoardParticipantDto?> RevertAsync(Guid standupId, DateOnly meetingDate, Guid personId, CancellationToken cancellationToken = default);

    Task<BoardParticipantDto?> SaveUpdateAsync(ParticipantUpdateDto update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records who is leading this date, or clears it when <paramref name="personId"/> is null. False means the person is not on the standup's leader
    /// roster and nothing was written, which is what a picker filled before someone was taken off that roster produces.
    /// </summary>
    Task<bool> SetLeaderAsync(Guid standupId, DateOnly meetingDate, Guid? personId, CancellationToken cancellationToken = default);

    /// <summary>Dates in the inclusive range on which someone presented, which the week strip marks so a month of standups is scannable.</summary>
    Task<IReadOnlyCollection<DateOnly>> GetPresentedDatesAsync(Guid standupId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
