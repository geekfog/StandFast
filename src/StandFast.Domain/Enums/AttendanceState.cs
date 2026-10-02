namespace StandFast.Domain.Enums;

/// <summary>Where a participant sits on the board for a single meeting date. The board renders one column per value.</summary>
public enum AttendanceState
{
    /// <summary>On the roster but not yet seen in the meeting.</summary>
    Roster = 0,

    /// <summary>Confirmed present and eligible to be called on.</summary>
    Available = 1,

    /// <summary>Has given their update.</summary>
    Presented = 2,

    /// <summary>Attending without presenting. Only someone who is not on the standup's presenter roster can be a guest; see <see cref="AttendeeKind"/>.</summary>
    Guest = 3,
}
