using StandFast.Application.Dtos;

namespace StandFast.Application.Services;

public interface IPersonService
{
    Task<IReadOnlyList<PersonDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<PersonEditDto?> GetForEditAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The person's profile photo, or null when they have none.</summary>
    Task<PersonPhotoDto?> GetPhotoAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> SaveAsync(PersonEditDto person, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
