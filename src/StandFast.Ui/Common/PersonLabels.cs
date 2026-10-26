namespace StandFast.Ui.Common;

/// <summary>Field labels for a person that appear on more than one screen, so the People table and the person dialog agree.</summary>
public static class PersonLabels
{
    public const string Title = "Title/Role";

    public const string StartDate = "Start date";

    public const string Email = "Email";

    public const string Supervisor = "Supervisor";

    /// <summary>Heading of the People screen's filter by roster: presenters, leaders, or neither.</summary>
    public const string RoleFilter = "Role";

    public static string Status(bool isActive) => isActive ? "Active" : "Inactive";
}
