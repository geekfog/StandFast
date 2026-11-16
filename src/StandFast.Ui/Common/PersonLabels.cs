namespace StandFast.Ui.Common;

/// <summary>Field labels for a person that appear on more than one screen, so the People table and the person dialog agree.</summary>
public static class PersonLabels
{
    public const string Title = "Title/Role";

    public const string StartDate = "Start date";

    public const string Email = "Email";

    public const string Supervisor = "Supervisor";

    /// <summary>Heading of the People screen's filter by roster: each roster role, or none of them.</summary>
    public const string RoleFilter = "Role";

    /// <summary>Label of the People screen's filter to one active standup's rosters.</summary>
    public const string StandupFilter = "Standup";

    public static string Status(bool isActive) => isActive ? "Active" : "Inactive";
}
