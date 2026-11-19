using Azure;
using Azure.Data.Tables;

namespace StandFast.Infrastructure.TableEntities;

/// <summary>One task, in its owner's partition.</summary>
public sealed class WorkTaskTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;

    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public string Title { get; set; } = string.Empty;

    public Guid StateId { get; set; }

    public int Priority { get; set; }

    /// <summary>The due date as a date key, since Table Storage has no date-only column type.</summary>
    public string? DueDate { get; set; }

    public Guid RequestedByPersonId { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }
}
