using StandFast.Domain.Entities;

namespace StandFast.Application.Dtos;

/// <param name="SelfFirstOnBoard">Lists the signed-in user's own card first in each board section rather than in its sorted place.</param>
public sealed record OrganizationSettingsDto(bool SelfFirstOnBoard)
{
    /// <summary>What the organization runs with until a setting is saved.</summary>
    public static readonly OrganizationSettingsDto Default = new(OrganizationSettings.DefaultSelfFirstOnBoard);
}
