namespace StandFast.Ui.Common;

/// <summary>Every route and query-string name the app links to. Referenced by both the <c>@page</c> directives and the navigation code.</summary>
public static class UiRoutes
{
    public const string Board = "/board";
    public const string People = "/people";
    public const string Standups = "/standups";

    public const string StandupQueryKey = "standupId";
    public const string DateQueryKey = "date";

    public const string SignIn = "/auth/login";
    public const string SignOut = "/auth/logout";

    /// <summary>Query-string key carrying where to send the user once sign-in completes.</summary>
    public const string ReturnUrlQueryKey = "returnUrl";

    public const string HealthCheck = "/healthz";
}
