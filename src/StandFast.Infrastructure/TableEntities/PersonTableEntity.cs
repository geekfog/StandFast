using Azure;
using Azure.Data.Tables;

namespace StandFast.Infrastructure.TableEntities;

/// <summary>Storage shape for a person. Kept separate from the domain entity so Azure types never leak upward.</summary>
public sealed class PersonTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;

    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? DisplayAs { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }
}
