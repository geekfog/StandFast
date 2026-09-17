using StandFast.Domain.Common;

namespace StandFast.Application.Dtos;

/// <summary>Create/update input for a person. <see cref="Id"/> is null when creating.</summary>
public sealed class PersonEditDto
{
    public Guid? Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>Optional. When set, this is how the person appears everywhere in the app instead of their first and last name.</summary>
    public string? DisplayAs { get; set; }

    /// <summary>Markdown source, up to <see cref="DomainLimits.MarkdownMaxLength"/> characters.</summary>
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}
