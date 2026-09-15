using StandFast.Domain.Common;

namespace StandFast.Application.Dtos;

/// <summary>Save input for the update/blockers editors. Deliberately narrower than <see cref="BoardParticipantDto"/>: attendance timestamps and the prior update are system-owned and not client settable.</summary>
public sealed class ParticipantUpdateDto
{
    public Guid StandupId { get; set; }

    public Guid PersonId { get; set; }

    public DateOnly MeetingDate { get; set; }

    /// <summary>Markdown source, up to <see cref="DomainLimits.MarkdownMaxLength"/> characters.</summary>
    public string? Update { get; set; }

    /// <summary>Markdown source, up to <see cref="DomainLimits.MarkdownMaxLength"/> characters.</summary>
    public string? Blockers { get; set; }
}
