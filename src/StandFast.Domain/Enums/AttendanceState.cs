namespace StandFast.Domain.Enums;

/// <summary>Where a participant sits on the board for a single meeting date. The board renders one section per value.</summary>
public enum AttendanceState
{
    /// <summary>On the roster but not yet seen in the meeting.</summary>
    Roster = 0,

    /// <summary>Confirmed present and eligible to be called on.</summary>
    Available = 1,

    /// <summary>Has given their update: a presenter, or a leader or guest at a standup that lets them present.</summary>
    Presented = 2,

    /// <summary>Attending as a guest, without presenting; see <see cref="AttendeeKind.Guest"/>.</summary>
    Guest = 3,

    /// <summary>Attending as a leader, without presenting; see <see cref="AttendeeKind.Leader"/>.</summary>
    Leader = 4,
}
