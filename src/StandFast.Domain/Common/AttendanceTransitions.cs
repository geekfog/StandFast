using StandFast.Domain.Enums;

namespace StandFast.Domain.Common;

/// <summary>The board's tap behaviour lives here so the UI, services, and tests agree on what a tap does.</summary>
public static class AttendanceTransitions
{
    /// <summary>
    /// Tapping moves a presenter one column to the right: roster to available, available to presented, and someone already presented stays there.
    /// Tapping a guest on the roster makes them a guest; a guest goes no further, since guests never present.
    /// </summary>
    public static AttendanceState Advance(AttendanceState state, AttendeeKind kind) => (kind, state) switch
    {
        (AttendeeKind.Guest, AttendanceState.Roster) => AttendanceState.Guest,
        (AttendeeKind.Guest, _) => state,
        (_, AttendanceState.Available or AttendanceState.Presented) => AttendanceState.Presented,
        _ => AttendanceState.Available,
    };

    /// <summary>Undo for a mis-tap: moves a presenter one column back, and a guest back to the roster.</summary>
    public static AttendanceState Revert(AttendanceState state, AttendeeKind kind) => (kind, state) switch
    {
        (AttendeeKind.Guest, AttendanceState.Guest) => AttendanceState.Roster,
        (AttendeeKind.Guest, _) => state,
        (_, AttendanceState.Presented) => AttendanceState.Available,
        _ => AttendanceState.Roster,
    };

    /// <summary>
    /// Where a stored state places someone of <paramref name="kind"/> on the board. A presenter cannot be a guest, so a guest entry recorded before they
    /// joined the presenter roster reads as not yet seen.
    /// </summary>
    public static AttendanceState Placement(AttendanceState state, AttendeeKind kind) =>
        kind == AttendeeKind.Presenter && state == AttendanceState.Guest ? AttendanceState.Roster : state;

    /// <summary>True when a guest can be shown in <paramref name="state"/>. Someone off the presenter roster who presented on a date before leaving it is not a guest on that date.</summary>
    public static bool IsGuestPlacement(AttendanceState state) => state is AttendanceState.Roster or AttendanceState.Guest;

    /// <summary>True once someone has been seen at the meeting, in whichever way they take part. Only an attending leader can be picked to lead.</summary>
    public static bool IsAttending(AttendanceState state) => state != AttendanceState.Roster;
}
