using StandFast.Application.Dtos;
using StandFast.Domain.Common;

namespace StandFast.Ui.Configuration;

/// <summary>Presentation settings bound from the <see cref="SectionName"/> configuration section.</summary>
public sealed class StandFastUiOptions
{
    public const string SectionName = "StandFastUi";

    /// <summary>
    /// Time zone for times that belong to no standup, such as backup timestamps, and for a standup whose own time zone the host cannot resolve.
    /// Falls back to the server time zone when unset.
    /// </summary>
    public string? DisplayTimeZoneId { get; set; }

    /// <summary>Blob container URI holding the shared ASP.NET Core Data Protection key ring. Required whenever more than one replica serves the app; see the README.</summary>
    public string? DataProtectionBlobUri { get; set; }

    /// <summary>Name shown on the navigation menu's environment tag. Deployed environments run as Production, so the deployment supplies their code here; unset falls back to the host environment name outside Production.</summary>
    public string? EnvironmentLabel { get; set; }

    /// <summary>The label for the environment tag, or null in production, where no tag is shown.</summary>
    public string? ResolveEnvironmentLabel(IHostEnvironment host) =>
        !string.IsNullOrWhiteSpace(EnvironmentLabel) ? EnvironmentLabel : host.IsProduction() ? null : host.EnvironmentName;

    public TimeZoneInfo ResolveTimeZone() =>
        string.IsNullOrWhiteSpace(DisplayTimeZoneId) ? TimeZoneInfo.Local : TimeZoneInfo.FindSystemTimeZoneById(DisplayTimeZoneId);

    /// <summary>The time zone a standup's board and reports are shown in: the standup's own, or <see cref="ResolveTimeZone()"/> when the host cannot resolve it.</summary>
    public TimeZoneInfo ResolveTimeZone(StandupDto? standup) => TimeZoneIds.Resolve(standup?.TimeZoneId) ?? ResolveTimeZone();
}
