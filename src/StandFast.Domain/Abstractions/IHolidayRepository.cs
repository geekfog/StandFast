using StandFast.Domain.Entities;

namespace StandFast.Domain.Abstractions;

public interface IHolidayRepository
{
    Task<IReadOnlyList<Holiday>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Holiday?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpsertAsync(Holiday holiday, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
