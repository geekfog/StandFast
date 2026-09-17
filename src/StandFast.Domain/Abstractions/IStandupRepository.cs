using StandFast.Domain.Entities;

namespace StandFast.Domain.Abstractions;

public interface IStandupRepository
{
    Task<IReadOnlyList<Standup>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Standup?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpsertAsync(Standup standup, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StandupMember>> GetMembersAsync(Guid standupId, CancellationToken cancellationToken = default);

    /// <summary>Every membership across every standup. Backs the People screen's list of standups per person, which cannot be answered from a single partition.</summary>
    Task<IReadOnlyList<StandupMember>> GetAllMembersAsync(CancellationToken cancellationToken = default);

    Task UpsertMemberAsync(StandupMember member, CancellationToken cancellationToken = default);

    Task RemoveMemberAsync(Guid standupId, Guid personId, CancellationToken cancellationToken = default);
}
