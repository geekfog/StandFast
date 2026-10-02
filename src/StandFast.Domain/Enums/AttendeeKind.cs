namespace StandFast.Domain.Enums;

/// <summary>
/// How someone takes part in one standup. Presenters are the standup's presenter roster; guests are anyone else in the people directory, so a visitor
/// is tracked without being assigned to the standup. Someone on the presenter roster is always a presenter there and can never be a guest.
/// </summary>
public enum AttendeeKind
{
    /// <summary>On the standup's presenter roster: marked present, then called on to give an update.</summary>
    Presenter = 0,

    /// <summary>Not on the presenter roster: attends without presenting, which includes a leader who is not also a presenter.</summary>
    Guest = 1,
}

public static class AttendeeKindExtensions
{
    /// <summary>Kinds in the order the board's roster filter lists them.</summary>
    public static IReadOnlyList<AttendeeKind> InDisplayOrder { get; } = [AttendeeKind.Presenter, AttendeeKind.Guest];
}
