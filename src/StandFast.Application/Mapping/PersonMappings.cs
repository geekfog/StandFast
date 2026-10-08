using StandFast.Application.Dtos;
using StandFast.Domain.Common;
using StandFast.Domain.Entities;

namespace StandFast.Application.Mapping;

/// <summary>The only place <see cref="Person"/> and its DTOs are converted, including input normalisation.</summary>
public static class PersonMappings
{
    public static PersonDto ToDto(this Person person) =>
        new(person.Id, person.FirstName, person.LastName, person.Email, person.IsActive, person.DisplayName, person.FullName, person.Initials, person.Notes, person.Title, person.Location, person.TimeZoneId, person.StartDate, person.PhotoSavedUtc);

    public static PersonEditDto ToEditDto(this Person person) => new()
    {
        Id = person.Id,
        FirstName = person.FirstName,
        LastName = person.LastName,
        Email = person.Email,
        DisplayAs = person.DisplayAs,
        Notes = person.Notes,
        Title = person.Title,
        Organization = person.Organization,
        Department = person.Department,
        City = person.City,
        StateOrRegion = person.StateOrRegion,
        TimeZoneId = person.TimeZoneId,
        StartDate = person.StartDate,
        IsActive = person.IsActive,
    };

    /// <summary>Copies editable fields onto the entity, each in its stored form, so what is persisted is consistent regardless of the caller.</summary>
    public static void ApplyTo(this PersonEditDto dto, Person person)
    {
        person.FirstName = dto.FirstName.Trim();
        person.LastName = dto.LastName.Trim();
        person.Email = EmailAddress.Normalise(dto.Email);

        // Blank and absent mean the same thing for each: fall back to the recorded name, hold no notes, and record no title, organization, department or location.
        person.DisplayAs = OptionalText.Normalize(dto.DisplayAs);
        person.Notes = OptionalText.Normalize(dto.Notes);
        person.Title = OptionalText.Normalize(dto.Title);
        person.Organization = OptionalText.Normalize(dto.Organization);
        person.Department = OptionalText.Normalize(dto.Department);
        person.City = OptionalText.Normalize(dto.City);
        person.StateOrRegion = OptionalText.Normalize(dto.StateOrRegion);
        person.TimeZoneId = TimeZoneIds.ToStoredOrNull(dto.TimeZoneId);
        person.StartDate = dto.StartDate;
        person.IsActive = dto.IsActive;
    }

    public static PersonPhotoDto ToDto(this PersonPhoto photo) => new(photo.ContentType, photo.Content);

    public static PersonPhoto ToEntity(this PersonPhotoDto photo, Guid personId) => new() { PersonId = personId, ContentType = photo.ContentType, Content = photo.Content };
}
