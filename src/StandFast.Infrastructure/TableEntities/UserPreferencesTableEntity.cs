using Azure;
using Azure.Data.Tables;

namespace StandFast.Infrastructure.TableEntities;

/// <summary>Storage shape for one user's settings. The subject identifier is kept as its own column as well as in the row key, so the value survives the key escaping intact.</summary>
public sealed class UserPreferencesTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;

    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string? Email { get; set; }

    public bool IsDarkMode { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }
}
