using Azure;
using Azure.Data.Tables;

namespace StandFast.Infrastructure.TableEntities;

public sealed class HolidayTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;

    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    /// <summary>The date as a date key, since Table Storage has no date-only column type.</summary>
    public string Date { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>The domain <c>HolidayDuration</c> enum as an integer, because Azure Tables has no enum type.</summary>
    public int Duration { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }
}
