using StandFast.Application.Dtos;
using StandFast.Domain.Enums;

namespace StandFast.Application.Mapping;

/// <summary>
/// Sort order for each board column. Each column answers a different question, so each sorts differently: the roster is a list you scan for a
/// name, while the other two are records of what happened and read best in the order it happened.
/// </summary>
public static class BoardColumnOrdering
{
    public static IReadOnlyList<BoardParticipantDto> InColumnOrder(this IEnumerable<BoardParticipantDto> participants, AttendanceState state) => state switch
    {
        // Oldest first, so the person who has been waiting longest to be called sits at the top.
        AttendanceState.Available =>
            [.. participants.OrderBy(participant => participant.MarkedAvailableUtc).ThenBy(participant => participant.DisplayName, StringComparer.OrdinalIgnoreCase)],

        // The order the standup actually ran in.
        AttendanceState.Presented =>
            [.. participants.OrderBy(participant => participant.PresentedUtc).ThenBy(participant => participant.DisplayName, StringComparer.OrdinalIgnoreCase)],

        // Always alphabetical by the name on screen, which is what someone scanning for a person expects.
        _ => [.. participants.OrderBy(participant => participant.DisplayName, StringComparer.OrdinalIgnoreCase)],
    };
}
