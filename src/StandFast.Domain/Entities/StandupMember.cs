namespace StandFast.Domain.Entities;

/// <summary>Membership of a <see cref="Person"/> in a <see cref="Standup"/>. The board roster is the set of active members.</summary>
public sealed class StandupMember
{
    public Guid StandupId { get; set; }

    public Guid PersonId { get; set; }

    /// <summary>Roster ordering. Ties fall back to display name so the order is always deterministic.</summary>
    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedUtc { get; set; }

    public string? ETag { get; set; }
}
