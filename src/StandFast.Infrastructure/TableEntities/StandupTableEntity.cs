using Azure;
using Azure.Data.Tables;

namespace StandFast.Infrastructure.TableEntities;

public sealed class StandupTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;

    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Bit flags from the domain <c>MeetingDays</c> enum. Stored as an integer because Azure Tables has no enum type.</summary>
    public int RecurrenceDays { get; set; }

    /// <summary>Local start time as minutes past midnight. Azure Tables has no time-of-day type.</summary>
    public int StartMinutesLocal { get; set; }

    public string TimeZoneId { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }
}
