using StandFast.Application.Dtos;
using StandFast.Domain.Enums;

namespace StandFast.Application.Mapping;

/// <summary>
/// Sort order for each board column. The roster and the people already present are both lists you scan for a name, so they share one alphabetical
/// order and a name sits in the same place whichever of the two it is in. Presented is a record of what happened and reads in the order it happened.
/// </summary>
public static class BoardColumnOrdering
{
    public static IReadOnlyList<BoardParticipantDto> InColumnOrder(this IEnumerable<BoardParticipantDto> participants, AttendanceState state) => state switch
    {
        // The order the standup actually ran in.
        AttendanceState.Presented =>
            [.. participants.OrderBy(participant => participant.PresentedUtc).ThenBy(participant => participant.DisplayName, StringComparer.OrdinalIgnoreCase)],

        // Alphabetical by the name on screen, which is what someone scanning for a person expects.
        _ => [.. participants.OrderBy(participant => participant.DisplayName, StringComparer.OrdinalIgnoreCase)],
    };
}
