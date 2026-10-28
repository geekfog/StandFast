namespace StandFast.Domain.Enums;

/// <summary>
/// How someone takes part in one standup, from the first roster they are on: presenter, then leader, then guest. Someone on the presenter roster is
/// always a presenter there, even when they also lead, so they never attend without presenting.
/// </summary>
public enum AttendeeKind
{
    /// <summary>On the standup's presenter roster: marked present, then called on to give an update.</summary>
    Presenter = 0,

    /// <summary>On the standup's guest roster, or a visitor on no roster at all who was recorded on a date: attends without presenting unless the standup lets guests present.</summary>
    Guest = 1,

    /// <summary>On the standup's leader roster but not its presenter roster: attends to run the meeting without presenting unless the standup lets leaders present.</summary>
    Leader = 2,
}

public static class AttendeeKindExtensions
{
    /// <summary>Kinds in the order the board's roster filter lists them.</summary>
    public static IReadOnlyList<AttendeeKind> InDisplayOrder { get; } = [AttendeeKind.Presenter, AttendeeKind.Leader, AttendeeKind.Guest];

    /// <summary>The roster that makes someone this kind of attendee. A visiting guest is on no roster, but is offered under the guest roster's name.</summary>
    public static RosterRole Roster(this AttendeeKind kind) => kind switch
    {
        AttendeeKind.Leader => RosterRole.Leader,
        AttendeeKind.Guest => RosterRole.Guest,
        _ => RosterRole.Presenter,
    };
}
