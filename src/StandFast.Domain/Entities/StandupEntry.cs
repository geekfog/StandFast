using StandFast.Domain.Enums;

namespace StandFast.Domain.Entities;

/// <summary>One participant's record for one standup on one meeting date: their attendance state, their update, and their blockers.</summary>
public sealed class StandupEntry
{
    public Guid StandupId { get; set; }

    public Guid PersonId { get; set; }

    public DateOnly MeetingDate { get; set; }

    public AttendanceState State { get; set; } = AttendanceState.Roster;

    public DateTimeOffset? MarkedAvailableUtc { get; set; }

    public DateTimeOffset? PresentedUtc { get; set; }

    /// <summary>Markdown source of the participant's update for this date.</summary>
    public string? Update { get; set; }

    /// <summary>Markdown source of the participant's blockers for this date.</summary>
    public string? Blockers { get; set; }

    /// <summary>When the update/blockers text was last saved. Null while only attendance has been recorded.</summary>
    public DateTimeOffset? UpdateSavedUtc { get; set; }

    public string? ETag { get; set; }

    /// <summary>True when the participant has typed anything worth carrying forward as a prior update.</summary>
    public bool HasContent => !string.IsNullOrWhiteSpace(Update) || !string.IsNullOrWhiteSpace(Blockers);
}
