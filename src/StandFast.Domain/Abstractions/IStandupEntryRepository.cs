using StandFast.Domain.Entities;

namespace StandFast.Domain.Abstractions;

public interface IStandupEntryRepository
{
    /// <summary>Fetches a participant's entry for <paramref name="meetingDate"/> together with their most recent earlier entry, which supplies the read-only "prior update" panel.</summary>
    Task<StandupEntryPair> GetCurrentAndPriorAsync(Guid standupId, Guid personId, DateOnly meetingDate, CancellationToken cancellationToken = default);

    Task UpsertAsync(StandupEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Meeting dates within the inclusive range on which at least one participant presented. Answers "which days hold a finished standup" for a
    /// whole standup in one query, which is what the week strip marks.
    /// </summary>
    Task<IReadOnlyCollection<DateOnly>> GetPresentedDatesAsync(Guid standupId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
