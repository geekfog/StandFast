using StandFast.Domain.Common;

namespace StandFast.Ui.Common;

/// <summary>Application identity strings used by the shell, page titles, and the Data Protection application name.</summary>
public static class AppInfo
{
    public const string Name = ApplicationInfo.Name;
    public const string Tagline = "Daily scrum tracking";
    public const string RepositoryUrl = "https://github.com/geekfog/StandFast";

    /// <summary>The app's icon under <c>wwwroot</c>, used as the browser tab icon and on the About page.</summary>
    public const string IconAsset = "favicon.svg";

    /// <summary>Suffix appended to every page title.</summary>
    public static string PageTitle(string page) => $"{page} · {Name}";
}
