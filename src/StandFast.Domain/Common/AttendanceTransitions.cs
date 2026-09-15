using StandFast.Domain.Enums;

namespace StandFast.Domain.Common;

/// <summary>The board's tap behaviour lives here so the UI, services, and tests agree on what a tap does.</summary>
public static class AttendanceTransitions
{
    /// <summary>Tapping a participant moves them one column to the right: roster to available, available to presented. Tapping someone already presented leaves them there.</summary>
    public static AttendanceState Advance(AttendanceState state) => state switch
    {
        AttendanceState.Roster => AttendanceState.Available,
        AttendanceState.Available => AttendanceState.Presented,
        _ => AttendanceState.Presented,
    };

    /// <summary>Undo for a mis-tap: moves one column back.</summary>
    public static AttendanceState Revert(AttendanceState state) => state switch
    {
        AttendanceState.Presented => AttendanceState.Available,
        AttendanceState.Available => AttendanceState.Roster,
        _ => AttendanceState.Roster,
    };
}
