using StandFast.Application.Auditing;
using StandFast.Application.Dtos;
using StandFast.Domain.Abstractions;
using StandFast.Domain.Entities;

namespace StandFast.Application.Services;

public interface IOrganizationSettingsService
{
    /// <summary>The organization's settings, with every unsaved setting at its default.</summary>
    Task<OrganizationSettingsDto> GetAsync(CancellationToken cancellationToken = default);

    Task SetSelfFirstOnBoardAsync(bool selfFirstOnBoard, CancellationToken cancellationToken = default);
}

public sealed class OrganizationSettingsService(IOrganizationSettingsRepository settings, IClock clock, IAuditLog audit) : IOrganizationSettingsService
{
    private const string AuditTargetType = nameof(OrganizationSettings);

    public async Task<OrganizationSettingsDto> GetAsync(CancellationToken cancellationToken = default) => ToDto(await settings.GetAsync(cancellationToken));

    public Task SetSelfFirstOnBoardAsync(bool selfFirstOnBoard, CancellationToken cancellationToken = default) =>
        SaveAsync(stored => stored.SelfFirstOnBoard = selfFirstOnBoard, $"{nameof(OrganizationSettings.SelfFirstOnBoard)} = {selfFirstOnBoard}", cancellationToken);

    /// <summary>Changes one setting, starting the record when none has been saved yet and leaving every other setting as it was.</summary>
    private async Task SaveAsync(Action<OrganizationSettings> change, string description, CancellationToken cancellationToken)
    {
        OrganizationSettings? stored = await settings.GetAsync(cancellationToken);
        OrganizationSettings entity = stored ?? new OrganizationSettings { CreatedUtc = clock.UtcNow };

        change(entity);
        entity.ModifiedUtc = stored is null ? null : clock.UtcNow;

        await settings.UpsertAsync(entity, cancellationToken);
        audit.Record(AuditEvents.OrganizationSettingsChanged, AuditTargetType, AuditTargetType, description);
    }

    private static OrganizationSettingsDto ToDto(OrganizationSettings? stored) => stored is null ? OrganizationSettingsDto.Default : new OrganizationSettingsDto(stored.SelfFirstOnBoard);
}
