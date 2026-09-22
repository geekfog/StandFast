using MudBlazor;
using StandFast.Domain.Enums;

namespace StandFast.Ui.Common;

/// <summary>The words and icon each roster role is shown by, so the People table's columns, the roster dialogs, and the board's leader picker all agree.</summary>
public static class RosterLabels
{
    /// <summary>Singular name of the role, as used in a dialog title or a field label.</summary>
    public static string Name(RosterRole role) => role switch
    {
        RosterRole.Leader => "Leader",
        _ => "Presenter",
    };

    /// <summary>Plural name of the role, as used for a column listing the people who hold it.</summary>
    public static string Plural(RosterRole role) => string.Concat(Name(role), "s");

    /// <summary>Title of the roster dialog and of the button that opens it.</summary>
    public static string RosterTitle(RosterRole role) => string.Concat(Name(role), " Roster");

    /// <summary>
    /// Icon on the button that opens the roster. The leader's is a gavel rather than another figure, because the roster buttons sit side by side at
    /// icon size and two people-shaped glyphs are indistinguishable there.
    /// </summary>
    public static string RosterIcon(RosterRole role) => role switch
    {
        RosterRole.Leader => Icons.Material.Filled.Gavel,
        _ => Icons.Material.Filled.Groups,
    };
}
