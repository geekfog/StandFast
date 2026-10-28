namespace StandFast.Domain.Enums;

/// <summary>The part a person plays in a standup. Each role is its own roster, so someone can hold any combination of them on one standup.</summary>
public enum RosterRole
{
    /// <summary>Gives an update at the standup. These are the people the board's columns hold.</summary>
    Presenter = 0,

    /// <summary>Eligible to run the standup. One of them can be picked as the leader of any single meeting date.</summary>
    Leader = 1,

    /// <summary>Expected to attend without presenting. Anyone in the directory can still visit the board as a guest; this roster records who is assigned to.</summary>
    Guest = 2,
}

public static class RosterRoleExtensions
{
    /// <summary>Roles in the order screens list them, so a table of a column per role and the buttons that open each roster come from one place.</summary>
    public static IReadOnlyList<RosterRole> InDisplayOrder { get; } = [RosterRole.Presenter, RosterRole.Leader, RosterRole.Guest];
}
