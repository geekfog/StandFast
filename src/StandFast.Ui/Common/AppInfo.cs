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

    /// <summary>Zero-padding applied to each part of the displayed version.</summary>
    private const string VersionPartFormat = "D2";

    /// <summary>
    /// The version built from <c>VersionPrefix</c> in <c>Directory.Build.props</c>, shown as <c>MM.mm</c>, with <c>.pp</c> appended once a patch has been released,
    /// so it reads the same as the <c>release/MM.mm</c> branch it was built from.
    /// </summary>
    public static readonly string Version = FormatVersion(typeof(AppInfo).Assembly.GetName().Version ?? new Version());

    private static string FormatVersion(Version version)
    {
        string majorMinor = $"{version.Major.ToString(VersionPartFormat)}.{version.Minor.ToString(VersionPartFormat)}";

        return version.Build > 0 ? $"{majorMinor}.{version.Build.ToString(VersionPartFormat)}" : majorMinor;
    }

    /// <summary>Suffix appended to every page title.</summary>
    public static string PageTitle(string page) => $"{page} · {Name}";
}
