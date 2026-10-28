using StandFast.Domain.Enums;

namespace StandFast.Domain.Common;

/// <summary>The board's tap behaviour lives here so the UI, services, and tests agree on what a tap does.</summary>
public static class AttendanceTransitions
{
    /// <summary>
    /// Tapping moves a presenter one column to the right: roster to available, available to presented, and someone already presented stays there.
    /// Tapping anyone else on the roster places them in their own kind's section. A second tap marks them as presented when <paramref name="presents"/>
    /// says the standup lets their kind present, and otherwise leaves them where they are.
    /// </summary>
    public static AttendanceState Advance(AttendanceState state, AttendeeKind kind, bool presents) => (kind, state) switch
    {
        (AttendeeKind.Presenter, AttendanceState.Available or AttendanceState.Presented) => AttendanceState.Presented,
        (AttendeeKind.Presenter, _) => AttendanceState.Available,
        (_, AttendanceState.Roster) => Attending(kind),
        _ when presents && state == Attending(kind) => AttendanceState.Presented,
        _ => state,
    };

    /// <summary>Undo for a mis-tap: moves a presenter one column back, anyone else who presented back to their own kind's section, and from there to the roster.</summary>
    public static AttendanceState Revert(AttendanceState state, AttendeeKind kind) => (kind, state) switch
    {
        (AttendeeKind.Presenter, AttendanceState.Presented) => AttendanceState.Available,
        (AttendeeKind.Presenter, _) => AttendanceState.Roster,
        (_, AttendanceState.Presented) => Attending(kind),
        _ when state == Attending(kind) => AttendanceState.Roster,
        _ => state,
    };

    /// <summary>The section someone of <paramref name="kind"/> lands in when first marked as here.</summary>
    public static AttendanceState Attending(AttendeeKind kind) => kind switch
    {
        AttendeeKind.Leader => AttendanceState.Leader,
        AttendeeKind.Guest => AttendanceState.Guest,
        _ => AttendanceState.Available,
    };

    /// <summary>
    /// Where a stored state places someone of <paramref name="kind"/> on the board. Attending without presenting follows the roster they are on now: a
    /// presenter cannot attend that way, so the entry reads as not yet seen, and anyone else is shown in their own kind's section.
    /// </summary>
    public static AttendanceState Placement(AttendanceState state, AttendeeKind kind) => state is AttendanceState.Guest or AttendanceState.Leader
        ? kind == AttendeeKind.Presenter ? AttendanceState.Roster : Attending(kind)
        : state;

    /// <summary>
    /// True when someone off the presenter roster can be shown in <paramref name="state"/>. Presented shows only while <paramref name="presents"/> says the
    /// standup lets their kind present, so someone who presented on a date before leaving the presenter roster is not shown on that date.
    /// </summary>
    public static bool IsNonPresenterPlacement(AttendanceState state, bool presents) =>
        state is AttendanceState.Roster or AttendanceState.Guest or AttendanceState.Leader || (presents && state == AttendanceState.Presented);

    /// <summary>True once someone has been seen at the meeting, in whichever way they take part. Only an attending leader can be picked to lead.</summary>
    public static bool IsAttending(AttendanceState state) => state != AttendanceState.Roster;

    /// <summary>True when someone belongs on the board in <paramref name="state"/>. An inactive person is never offered on the roster, but still shows on a date they attended.</summary>
    public static bool IsOnBoard(AttendanceState state, bool isActive) => isActive || IsAttending(state);
}
