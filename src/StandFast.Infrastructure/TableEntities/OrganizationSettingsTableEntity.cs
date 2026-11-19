using Azure;
using Azure.Data.Tables;

namespace StandFast.Infrastructure.TableEntities;

public sealed class OrganizationSettingsTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;

    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    /// <summary>Nullable so a row saved before the setting existed reads as its default.</summary>
    public bool? SelfFirstOnBoard { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }
}
