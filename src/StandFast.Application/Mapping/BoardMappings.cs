using StandFast.Application.Dtos;
using StandFast.Domain.Entities;
using StandFast.Domain.Enums;

namespace StandFast.Application.Mapping;

public static class BoardMappings
{
    /// <summary>Composes one board tile from the roster entry, the person, and the current/prior entry pair returned by storage.</summary>
    public static BoardParticipantDto ToParticipantDto(this StandupEntryPair pair, StandupMember member, Person person)
    {
        StandupEntry? current = pair.Current;
        StandupEntry? prior = pair.Prior;

        return new BoardParticipantDto(
            person.Id,
            person.DisplayName,
            person.Initials,
            person.Email,
            member.DisplayOrder,
            current?.State ?? AttendanceState.Roster,
            current?.MarkedAvailableUtc,
            current?.PresentedUtc,
            current?.Update,
            current?.Blockers,
            current?.UpdateSavedUtc,
            prior?.Update,
            prior?.Blockers,
            prior?.MeetingDate,
            prior?.UpdateSavedUtc);
    }

    /// <summary>Creates the entry a mutation writes to, reusing the stored one when it exists so timestamps and text survive.</summary>
    public static StandupEntry EnsureEntry(this StandupEntryPair pair, Guid standupId, Guid personId, DateOnly meetingDate) =>
        pair.Current ?? new StandupEntry { StandupId = standupId, PersonId = personId, MeetingDate = meetingDate };
}
