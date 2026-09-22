using StandFast.Application.Dtos;
using StandFast.Domain.Common;
using StandFast.Domain.Entities;

namespace StandFast.Application.Mapping;

/// <summary>The only place <see cref="Person"/> and its DTOs are converted, including input normalisation.</summary>
public static class PersonMappings
{
    public static PersonDto ToDto(this Person person) =>
        new(person.Id, person.FirstName, person.LastName, person.Email, person.IsActive, person.DisplayName, person.FullName, person.Initials, person.Notes);

    public static PersonEditDto ToEditDto(this Person person) => new()
    {
        Id = person.Id,
        FirstName = person.FirstName,
        LastName = person.LastName,
        Email = person.Email,
        DisplayAs = person.DisplayAs,
        Notes = person.Notes,
        IsActive = person.IsActive,
    };

    /// <summary>Copies editable fields onto the entity, each in its stored form, so what is persisted is consistent regardless of the caller.</summary>
    public static void ApplyTo(this PersonEditDto dto, Person person)
    {
        person.FirstName = dto.FirstName.Trim();
        person.LastName = dto.LastName.Trim();
        person.Email = EmailAddress.Normalise(dto.Email);

        // Blank and absent mean the same thing for both: fall back to the recorded name, and hold no notes.
        person.DisplayAs = Normalise(dto.DisplayAs);
        person.Notes = Normalise(dto.Notes);
        person.IsActive = dto.IsActive;
    }

    private static string? Normalise(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
