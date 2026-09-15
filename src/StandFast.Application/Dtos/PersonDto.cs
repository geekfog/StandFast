namespace StandFast.Application.Dtos;

/// <summary>Read model for a person. <see cref="DisplayName"/> and <see cref="Initials"/> are projected from the domain so name rendering stays defined in one place.</summary>
public sealed record PersonDto(Guid Id, string FirstName, string LastName, string Email, bool IsActive, string DisplayName, string Initials);
