using StandFast.Domain.Enums;

namespace StandFast.Application.Dtos;

/// <summary>Everything the board needs for one participant on one date, including the prior update that the "copy forward" action pulls from.</summary>
public sealed record BoardParticipantDto(
    Guid PersonId,
    string DisplayName,
    string Initials,
    string Email,
    string? Notes,
    string? Title,
    string Location,
    string? TimeZoneId,
    DateTimeOffset? PhotoSavedUtc,
    int DisplayOrder,
    AttendanceState State,
    DateTimeOffset? MarkedAvailableUtc,
    DateTimeOffset? PresentedUtc,
    string? Update,
    string? Blockers,
    string? ParkingLot,
    DateTimeOffset? UpdateSavedUtc,
    string? PriorUpdate,
    string? PriorBlockers,
    string? PriorParkingLot,
    DateOnly? PriorMeetingDate,
    DateTimeOffset? PriorSavedUtc) : IRosterOrdered
{
    public bool HasPrior => PriorMeetingDate is not null && (!string.IsNullOrWhiteSpace(PriorUpdate) || !string.IsNullOrWhiteSpace(PriorBlockers) || !string.IsNullOrWhiteSpace(PriorParkingLot));

    /// <summary>True once something has been recorded for this meeting date. The board colours the open-update action from this, so a glance shows who has already written theirs.</summary>
    public bool HasUpdate => !string.IsNullOrWhiteSpace(Update) || !string.IsNullOrWhiteSpace(Blockers) || !string.IsNullOrWhiteSpace(ParkingLot);
}
