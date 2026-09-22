namespace StandFast.Domain.Enums;

/// <summary>The part a person plays in a standup. Each role is its own roster, so someone can lead a standup without presenting at it and the other way round.</summary>
public enum RosterRole
{
    /// <summary>Gives an update at the standup. These are the people the board's columns hold.</summary>
    Presenter = 0,

    /// <summary>Eligible to run the standup. One of them can be picked as the leader of any single meeting date.</summary>
    Leader = 1,
}

public static class RosterRoleExtensions
{
    /// <summary>Roles in the order screens list them, so a table of a column per role and the buttons that open each roster come from one place.</summary>
    public static IReadOnlyList<RosterRole> InDisplayOrder { get; } = [RosterRole.Presenter, RosterRole.Leader];
}
