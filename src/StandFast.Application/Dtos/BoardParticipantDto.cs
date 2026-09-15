using StandFast.Domain.Enums;

namespace StandFast.Application.Dtos;

/// <summary>Everything the board needs for one participant on one date, including the prior update that the "copy forward" action pulls from.</summary>
public sealed record BoardParticipantDto(
    Guid PersonId,
    string DisplayName,
    string Initials,
    string Email,
    int DisplayOrder,
    AttendanceState State,
    DateTimeOffset? MarkedAvailableUtc,
    DateTimeOffset? PresentedUtc,
    string? Update,
    string? Blockers,
    DateTimeOffset? UpdateSavedUtc,
    string? PriorUpdate,
    string? PriorBlockers,
    DateOnly? PriorMeetingDate,
    DateTimeOffset? PriorSavedUtc) : IRosterOrdered
{
    public bool HasPrior => PriorMeetingDate is not null && (!string.IsNullOrWhiteSpace(PriorUpdate) || !string.IsNullOrWhiteSpace(PriorBlockers));
}
