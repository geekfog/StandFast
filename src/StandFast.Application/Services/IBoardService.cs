using StandFast.Application.Dtos;

namespace StandFast.Application.Services;

public interface IBoardService
{
    Task<StandupBoardDto> GetBoardAsync(Guid standupId, DateOnly meetingDate, CancellationToken cancellationToken = default);

    /// <summary>Tap forward: roster to available, available to presented. Throws <see cref="BoardLockedException"/> once the date has been locked.</summary>
    Task<BoardParticipantDto?> AdvanceAsync(Guid standupId, DateOnly meetingDate, Guid personId, CancellationToken cancellationToken = default);

    /// <summary>Undo a tap by moving the participant one column back.</summary>
    Task<BoardParticipantDto?> RevertAsync(Guid standupId, DateOnly meetingDate, Guid personId, CancellationToken cancellationToken = default);

    Task<BoardParticipantDto?> SaveUpdateAsync(ParticipantUpdateDto update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records who is leading this date, or clears it when <paramref name="personId"/> is null. False means the person is not on the standup's leader
    /// roster and nothing was written, which is what a picker filled before someone was taken off that roster produces.
    /// </summary>
    Task<bool> SetLeaderAsync(Guid standupId, DateOnly meetingDate, Guid? personId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes this date to changes and returns the moment the lock records. That moment is the current time during the standup and the day's last turn
    /// plus a short tail once the standup is long over; see <see cref="Domain.Common.BoardLockPolicy"/>. Locking a date already locked keeps the
    /// original moment, so a second press cannot rewrite when the standup closed.
    /// </summary>
    Task<DateTimeOffset?> LockAsync(Guid standupId, DateOnly meetingDate, CancellationToken cancellationToken = default);

    /// <summary>Reopens a locked date. A date that is not locked is left alone.</summary>
    Task UnlockAsync(Guid standupId, DateOnly meetingDate, CancellationToken cancellationToken = default);

    /// <summary>Dates in the inclusive range on which someone presented, which the week strip marks so a month of standups is scannable.</summary>
    Task<IReadOnlyCollection<DateOnly>> GetPresentedDatesAsync(Guid standupId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
