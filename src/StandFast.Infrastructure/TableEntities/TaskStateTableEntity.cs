using Azure;
using Azure.Data.Tables;

namespace StandFast.Infrastructure.TableEntities;

public sealed class TaskStateTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;

    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>The domain <c>TaskStateGroup</c> enum as an integer, because Azure Tables has no enum type.</summary>
    public int Group { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }
}
