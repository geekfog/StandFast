namespace StandFast.Ui.Configuration;

/// <summary>Presentation settings bound from the <see cref="SectionName"/> configuration section.</summary>
public sealed class StandFastUiOptions
{
    public const string SectionName = "StandFastUi";

    /// <summary>Time zone the board treats as "today" and renders meeting times in. Falls back to the server time zone when unset.</summary>
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
}
