using StandFast.Domain.Common;
using StandFast.Domain.Entities;

namespace StandFast.Domain.Abstractions;

public interface IPersonRepository
{
    Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Person?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpsertAsync(Person person, CancellationToken cancellationToken = default);

    /// <summary>Removes the person, their profile photo, and their days off.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PersonPhoto?> GetPhotoAsync(Guid personId, CancellationToken cancellationToken = default);

    Task UpsertPhotoAsync(PersonPhoto photo, CancellationToken cancellationToken = default);

    Task DeletePhotoAsync(Guid personId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersonDayOff>> GetDaysOffAsync(Guid personId, CancellationToken cancellationToken = default);

    /// <summary>Everyone's days off that cover at least one date in the inclusive range, which is what the board weighs against a sprint.</summary>
    Task<IReadOnlyList<PersonDayOff>> GetDaysOffAsync(DateRange dates, CancellationToken cancellationToken = default);

    Task UpsertDayOffAsync(PersonDayOff dayOff, CancellationToken cancellationToken = default);

    Task DeleteDayOffAsync(Guid personId, Guid dayOffId, CancellationToken cancellationToken = default);
}
