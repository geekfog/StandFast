using Azure;
using Azure.Data.Tables;

namespace StandFast.Infrastructure.TableEntities;

/// <summary>Storage shape for one standup's record of one meeting date. The row key is the date itself, so a meeting is a point read.</summary>
public sealed class StandupMeetingTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>Meeting date in <c>yyyyMMdd</c> form, which sorts chronologically and reads straight out of the key.</summary>
    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public Guid? LeaderPersonId { get; set; }

    public DateTimeOffset? LockedUtc { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }
}
