using Azure;
using Azure.Data.Tables;

namespace StandFast.Infrastructure.TableEntities;

public sealed class StandupEntryTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>Inverted meeting date; see <c>StorageKeys.EntryRowKey</c> for why.</summary>
    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public Guid StandupId { get; set; }

    public Guid PersonId { get; set; }

    /// <summary>Meeting date in <c>yyyyMMdd</c> form. Redundant with the row key, but makes rows readable in Storage Explorer and filterable without decoding.</summary>
    public string MeetingDateKey { get; set; } = string.Empty;

    /// <summary>Numeric value of the domain <c>AttendanceState</c> enum.</summary>
    public int State { get; set; }

    public DateTimeOffset? MarkedAvailableUtc { get; set; }

    public DateTimeOffset? PresentedUtc { get; set; }

    public string? Update { get; set; }

    public string? Blockers { get; set; }

    public DateTimeOffset? UpdateSavedUtc { get; set; }
}
