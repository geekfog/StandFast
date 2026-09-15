using StandFast.Domain.Entities;

namespace StandFast.Domain.Abstractions;

public interface IPersonRepository
{
    Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Person?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpsertAsync(Person person, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
