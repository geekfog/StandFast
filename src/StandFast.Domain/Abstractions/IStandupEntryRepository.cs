using StandFast.Domain.Entities;

namespace StandFast.Domain.Abstractions;

public interface IStandupEntryRepository
{
    /// <summary>Fetches a participant's entry for <paramref name="meetingDate"/> together with their most recent earlier entry, which supplies the read-only "prior update" panel.</summary>
    Task<StandupEntryPair> GetCurrentAndPriorAsync(Guid standupId, Guid personId, DateOnly meetingDate, CancellationToken cancellationToken = default);

    Task UpsertAsync(StandupEntry entry, CancellationToken cancellationToken = default);
}
