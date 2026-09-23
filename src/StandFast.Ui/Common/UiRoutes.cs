namespace StandFast.Ui.Common;

/// <summary>Every route and query-string name the app links to. Referenced by both the <c>@page</c> directives and the navigation code.</summary>
public static class UiRoutes
{
    public const string Board = "/board";
    public const string People = "/people";
    public const string Standups = "/standups";
    public const string Backup = "/backup";
    public const string Reports = "/reports";
    public const string About = "/about";

    /// <summary>Endpoint that streams the backup file. Separate from the page because a download is a plain HTTP response with its own headers, not a Blazor navigation.</summary>
    public const string BackupDownload = "/backup/download";

    public const string StandupQueryKey = "standupId";
    public const string DateQueryKey = "date";

    /// <summary>Which report the reports page is showing, and how many days back it covers. Both live in the URL so a report view can be shared.</summary>
    public const string ReportQueryKey = "report";
    public const string DaysQueryKey = "days";

    public const string SignIn = "/auth/login";
    public const string SignOut = "/auth/logout";

    /// <summary>Query-string key carrying where to send the user once sign-in completes.</summary>
    public const string ReturnUrlQueryKey = "returnUrl";

    public const string HealthCheck = "/healthz";
}
