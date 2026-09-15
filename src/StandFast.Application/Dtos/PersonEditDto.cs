namespace StandFast.Application.Dtos;

/// <summary>Create/update input for a person. <see cref="Id"/> is null when creating.</summary>
public sealed class PersonEditDto
{
    public Guid? Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
