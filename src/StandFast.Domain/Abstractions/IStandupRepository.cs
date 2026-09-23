using StandFast.Domain.Entities;
using StandFast.Domain.Enums;

namespace StandFast.Domain.Abstractions;

public interface IStandupRepository
{
    Task<IReadOnlyList<Standup>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Standup?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpsertAsync(Standup standup, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StandupMember>> GetMembersAsync(Guid standupId, RosterRole role, CancellationToken cancellationToken = default);

    /// <summary>Every membership in one role across every standup. Backs the People screen's column for that role, which cannot be answered from a single partition.</summary>
    Task<IReadOnlyList<StandupMember>> GetAllMembersAsync(RosterRole role, CancellationToken cancellationToken = default);

    Task UpsertMemberAsync(StandupMember member, CancellationToken cancellationToken = default);

    Task RemoveMemberAsync(Guid standupId, Guid personId, RosterRole role, CancellationToken cancellationToken = default);

    /// <summary>What has been recorded about one standup on one date, or null while nothing has been.</summary>
    Task<StandupMeeting?> GetMeetingAsync(Guid standupId, DateOnly meetingDate, CancellationToken cancellationToken = default);

    Task UpsertMeetingAsync(StandupMeeting meeting, CancellationToken cancellationToken = default);

    /// <summary>
    /// Meeting dates within the inclusive range that have been locked. Answers "which days of this week are closed" for a whole standup in one
    /// query, which is what the week strip marks.
    /// </summary>
    Task<IReadOnlyCollection<DateOnly>> GetLockedDatesAsync(Guid standupId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
