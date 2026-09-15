namespace StandFast.Ui.Common;

/// <summary>Application identity strings used by the shell, page titles, and the Data Protection application name.</summary>
public static class AppInfo
{
    public const string Name = "StandFast";
    public const string Tagline = "Daily scrum tracking";

    /// <summary>Suffix appended to every page title.</summary>
    public static string PageTitle(string page) => $"{page} · {Name}";
}
