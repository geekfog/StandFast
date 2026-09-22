using StandFast.Application.Dtos;
using StandFast.Domain.Entities;

namespace StandFast.Application.Mapping;

public static class StandupMappings
{
    public static StandupDto ToDto(this Standup standup) =>
        new(standup.Id, standup.Name, standup.Description, standup.RecurrenceDays, standup.StartTimeLocal, standup.TimeZoneId, standup.IsActive);

    public static StandupEditDto ToEditDto(this Standup standup) => new()
    {
        Id = standup.Id,
        Name = standup.Name,
        Description = standup.Description,
        RecurrenceDays = standup.RecurrenceDays,
        StartTimeLocal = standup.StartTimeLocal,
        TimeZoneId = standup.TimeZoneId,
        IsActive = standup.IsActive,
    };

    public static void ApplyTo(this StandupEditDto dto, Standup standup)
    {
        standup.Name = dto.Name.Trim();
        standup.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        standup.RecurrenceDays = dto.RecurrenceDays;
        standup.StartTimeLocal = dto.StartTimeLocal;
        standup.TimeZoneId = dto.TimeZoneId;
        standup.IsActive = dto.IsActive;
    }

    public static StandupMemberDto ToDto(this StandupMember member, Person person) =>
        new(member.StandupId, member.PersonId, person.DisplayName, person.Email, member.DisplayOrder, member.IsActive);

    /// <summary>
    /// One roster as the screens show it: memberships joined to their people, in roster order. A membership whose person record has gone is dropped,
    /// which is what an interrupted delete leaves behind.
    /// </summary>
    public static IReadOnlyList<StandupMemberDto> ToRoster(this IEnumerable<StandupMember> members, IReadOnlyDictionary<Guid, Person> peopleById) =>
        members.Where(member => peopleById.ContainsKey(member.PersonId)).Select(member => member.ToDto(peopleById[member.PersonId])).InRosterOrder();
}
