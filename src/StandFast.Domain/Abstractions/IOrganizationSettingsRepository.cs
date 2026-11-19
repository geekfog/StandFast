using StandFast.Domain.Entities;

namespace StandFast.Domain.Abstractions;

public interface IOrganizationSettingsRepository
{
    /// <summary>The one settings record, or null until it is first saved.</summary>
    Task<OrganizationSettings?> GetAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(OrganizationSettings settings, CancellationToken cancellationToken = default);
}
