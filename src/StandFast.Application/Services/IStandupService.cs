using StandFast.Application.Dtos;
using StandFast.Domain.Enums;

namespace StandFast.Application.Services;

public interface IStandupService
{
    Task<IReadOnlyList<StandupDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Active standups only. Backs the board's standup picker.</summary>
    Task<IReadOnlyList<StandupDto>> GetSelectableAsync(CancellationToken cancellationToken = default);

    Task<StandupEditDto?> GetForEditAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> SaveAsync(StandupEditDto standup, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StandupMemberDto>> GetMembersAsync(Guid standupId, RosterRole role, CancellationToken cancellationToken = default);

    /// <summary>Names of the standups each person is on in each role, sorted alphabetically. Kept alongside the people rather than on them, since it belongs to the standups.</summary>
    Task<StandupNamesByPersonDto> GetStandupNamesByPersonAsync(CancellationToken cancellationToken = default);

    Task AddMemberAsync(Guid standupId, Guid personId, RosterRole role, CancellationToken cancellationToken = default);

    Task RemoveMemberAsync(Guid standupId, Guid personId, RosterRole role, CancellationToken cancellationToken = default);

    Task SetMemberOrderAsync(Guid standupId, Guid personId, RosterRole role, int displayOrder, CancellationToken cancellationToken = default);
}
