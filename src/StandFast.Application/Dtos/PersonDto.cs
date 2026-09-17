namespace StandFast.Application.Dtos;

/// <summary>
/// Read model for a person. <see cref="DisplayName"/> and <see cref="Initials"/> are projected from the domain so name rendering stays defined
/// in one place, and <see cref="DisplayName"/> already reflects any display override.
/// </summary>
public sealed record PersonDto(Guid Id, string FirstName, string LastName, string Email, bool IsActive, string DisplayName, string FullName, string Initials, string? Notes)
{
    /// <summary>True when the person is shown under something other than their recorded name, which the admin screen points out.</summary>
    public bool HasDisplayOverride => !string.Equals(DisplayName, FullName, StringComparison.Ordinal);

    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);
}
