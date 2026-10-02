using StandFast.Application.Dtos;
using StandFast.Domain.Common;
using StandFast.Domain.Entities;
using StandFast.Domain.Enums;

namespace StandFast.Application.Mapping;

public static class BoardMappings
{
    /// <summary>Guests are not on a roster, so they carry no roster position of their own and the board orders them by name alone.</summary>
    public const int GuestDisplayOrder = 0;

    /// <summary>Composes one board tile from the person, how they take part, their roster position, and the current/prior entry pair returned by storage.</summary>
    public static BoardParticipantDto ToParticipantDto(this StandupEntryPair pair, Person person, AttendeeKind kind, int displayOrder)
    {
        StandupEntry? current = pair.Current;
        StandupEntry? prior = pair.Prior;

        return new BoardParticipantDto(
            person.Id,
            person.DisplayName,
            person.Initials,
            person.Email,
            person.Notes,
            person.Title,
            person.Location,
            person.TimeZoneId,
            person.PhotoSavedUtc,
            displayOrder,
            kind,
            AttendanceTransitions.Placement(current?.State ?? AttendanceState.Roster, kind),
            current?.MarkedAvailableUtc,
            current?.PresentedUtc,
            current?.Update,
            current?.Blockers,
            current?.ParkingLot,
            current?.UpdateSavedUtc,
            prior?.Update,
            prior?.Blockers,
            prior?.ParkingLot,
            prior?.MeetingDate,
            prior?.UpdateSavedUtc);
    }

    /// <summary>Creates the entry a mutation writes to, reusing the stored one when it exists so timestamps and text survive.</summary>
    public static StandupEntry EnsureEntry(this StandupEntryPair pair, Guid standupId, Guid personId, DateOnly meetingDate) =>
        pair.Current ?? new StandupEntry { StandupId = standupId, PersonId = personId, MeetingDate = meetingDate };
}
