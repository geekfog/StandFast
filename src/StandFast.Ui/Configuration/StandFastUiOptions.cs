namespace StandFast.Ui.Configuration;

/// <summary>Presentation settings bound from the <see cref="SectionName"/> configuration section.</summary>
public sealed class StandFastUiOptions
{
    public const string SectionName = "StandFastUi";

    /// <summary>Time zone the board treats as "today" and renders meeting times in. Falls back to the server time zone when unset.</summary>
    public string? DisplayTimeZoneId { get; set; }

    /// <summary>Blob container URI holding the shared ASP.NET Core Data Protection key ring. Required whenever more than one replica serves the app; see the README.</summary>
    public string? DataProtectionBlobUri { get; set; }

    public TimeZoneInfo ResolveTimeZone() =>
        string.IsNullOrWhiteSpace(DisplayTimeZoneId) ? TimeZoneInfo.Local : TimeZoneInfo.FindSystemTimeZoneById(DisplayTimeZoneId);
}
