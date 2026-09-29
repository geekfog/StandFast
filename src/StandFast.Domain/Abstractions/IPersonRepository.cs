using StandFast.Domain.Entities;

namespace StandFast.Domain.Abstractions;

public interface IPersonRepository
{
    Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Person?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpsertAsync(Person person, CancellationToken cancellationToken = default);

    /// <summary>Removes the person and their profile photo.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PersonPhoto?> GetPhotoAsync(Guid personId, CancellationToken cancellationToken = default);

    Task UpsertPhotoAsync(PersonPhoto photo, CancellationToken cancellationToken = default);

    Task DeletePhotoAsync(Guid personId, CancellationToken cancellationToken = default);
}
