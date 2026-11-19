namespace StandFast.Domain.Entities;

/// <summary>Settings that apply to everyone in the organization. There is one record; until it is first saved, every setting has its default.</summary>
public sealed class OrganizationSettings
{
    public const bool DefaultSelfFirstOnBoard = false;

    /// <summary>Lists the signed-in user's own card, the one marked with a star, first in each board section rather than in its sorted place.</summary>
    public bool SelfFirstOnBoard { get; set; } = DefaultSelfFirstOnBoard;

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }

    public string? ETag { get; set; }
}
