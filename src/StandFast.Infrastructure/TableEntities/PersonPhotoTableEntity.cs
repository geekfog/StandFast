using Azure;
using Azure.Data.Tables;

namespace StandFast.Infrastructure.TableEntities;

/// <summary>Storage shape for a profile photo. One row per person, keyed the same way as the person's own row.</summary>
public sealed class PersonPhotoTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;

    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public string ContentType { get; set; } = string.Empty;

    public byte[] Content { get; set; } = [];
}
