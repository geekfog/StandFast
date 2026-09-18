using System.Globalization;
using Azure;
using Azure.Data.Tables;
using StandFast.Application.Abstractions;
using StandFast.Application.Backup;
using StandFast.Infrastructure.Storage;

namespace StandFast.Infrastructure.Backup;

/// <summary>
/// Copies whole tables in and out as untyped rows. Working in columns rather than through the typed table entities is deliberate: the backup then
/// carries whatever the tables actually hold, so a column added to an entity needs no change here and a backup taken before that column still restores.
/// </summary>
public sealed class TableBackupStore(ITableClientProvider tables) : IBackupStore
{
    /// <summary>Azure Tables allows at most this many actions in one transaction, and every action in it must share a partition key.</summary>
    private const int TransactionBatchSize = 100;

    /// <summary>Round-trip formats, so a value read back parses to exactly what was exported.</summary>
    private const string RoundTripNumberFormat = "R";

    private const string RoundTripDateFormat = "O";

    private const string GuidFormat = "D";

    /// <summary>Name of the etag column the table service returns alongside a queried row.</summary>
    private const string ETagColumn = "odata.etag";

    /// <summary>Columns the table service maintains itself. They are neither exported nor written back, because the service assigns them on write.</summary>
    private static readonly string[] ServiceColumns = [nameof(ITableEntity.Timestamp), ETagColumn];

    /// <summary>Clearing a table needs only the keys, so the delete pass asks for nothing else.</summary>
    private static readonly string[] KeyColumns = [nameof(ITableEntity.PartitionKey), nameof(ITableEntity.RowKey)];

    public IReadOnlyList<string> TableNames => StorageNames.DataTables;

    public async Task<IReadOnlyList<BackupTable>> ExportAsync(CancellationToken cancellationToken = default)
    {
        List<BackupTable> exported = [];

        foreach (string logicalName in StorageNames.DataTables)
        {
            TableClient client = await tables.GetAsync(logicalName, cancellationToken);
            List<IReadOnlyDictionary<string, BackupValue>> rows = [];

            await foreach (TableEntity entity in client.QueryAsync<TableEntity>(cancellationToken: cancellationToken))
            {
                rows.Add(ToBackupRow(entity));
            }

            exported.Add(new BackupTable(logicalName, rows));
        }

        return exported;
    }

    /// <summary>
    /// Each table is emptied and then refilled. Azure Tables has no transaction spanning tables or partitions, so the work is done one table and
    /// one partition batch at a time; a failure part-way therefore leaves the remaining tables as they were rather than half-merged.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, int>> ReplaceAsync(IReadOnlyList<BackupTable> backupTables, CancellationToken cancellationToken = default)
    {
        Dictionary<string, int> written = new(StringComparer.Ordinal);

        foreach (string logicalName in StorageNames.DataTables)
        {
            TableClient client = await tables.GetAsync(logicalName, cancellationToken);
            IReadOnlyList<IReadOnlyDictionary<string, BackupValue>> rows =
                backupTables.FirstOrDefault(table => string.Equals(table.Name, logicalName, StringComparison.Ordinal))?.Rows ?? [];

            // The rows are converted before anything is deleted, so a malformed backup is rejected while the table still holds its data.
            List<TableTransactionAction> writes = [.. rows.Select(row => new TableTransactionAction(TableTransactionActionType.UpsertReplace, ToTableEntity(row)))];

            await ClearAsync(client, cancellationToken);
            await SubmitAsync(client, writes, cancellationToken);

            written[logicalName] = rows.Count;
        }

        return written;
    }

    private static async Task ClearAsync(TableClient client, CancellationToken cancellationToken)
    {
        List<TableTransactionAction> deletes = [];

        await foreach (TableEntity entity in client.QueryAsync<TableEntity>(select: KeyColumns, cancellationToken: cancellationToken))
        {
            // ETag.All, because the row is being discarded outright: a concurrent edit must not leave part of the replaced data behind.
            deletes.Add(new TableTransactionAction(TableTransactionActionType.Delete, entity, ETag.All));
        }

        await SubmitAsync(client, deletes, cancellationToken);
    }

    private static async Task SubmitAsync(TableClient client, IEnumerable<TableTransactionAction> actions, CancellationToken cancellationToken)
    {
        foreach (IGrouping<string, TableTransactionAction> partition in actions.GroupBy(action => action.Entity.PartitionKey, StringComparer.Ordinal))
        {
            foreach (TableTransactionAction[] batch in partition.Chunk(TransactionBatchSize))
            {
                await client.SubmitTransactionAsync(batch, cancellationToken);
            }
        }
    }

    private static IReadOnlyDictionary<string, BackupValue> ToBackupRow(TableEntity entity)
    {
        Dictionary<string, BackupValue> row = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, object> column in entity)
        {
            if (column.Value is null || ServiceColumns.Contains(column.Key, StringComparer.Ordinal))
            {
                continue;
            }

            row[column.Key] = ToBackupValue(column.Key, column.Value);
        }

        return row;
    }

    private static TableEntity ToTableEntity(IReadOnlyDictionary<string, BackupValue> row)
    {
        TableEntity entity = new();

        foreach (KeyValuePair<string, BackupValue> column in row)
        {
            if (ServiceColumns.Contains(column.Key, StringComparer.Ordinal))
            {
                continue;
            }

            entity[column.Key] = ToStorageValue(column.Key, column.Value);
        }

        foreach (string key in KeyColumns)
        {
            if (!entity.TryGetValue(key, out object? value) || value is not string text || text.Length == 0)
            {
                throw new BackupFormatException($"A row in the backup has no usable {key}, so it cannot be identified in storage.");
            }
        }

        return entity;
    }

    private static BackupValue ToBackupValue(string column, object value) => value switch
    {
        string text => new BackupValue(BackupValueKind.String, text),
        bool flag => new BackupValue(BackupValueKind.Boolean, flag.ToString()),
        int number => new BackupValue(BackupValueKind.Int32, number.ToString(CultureInfo.InvariantCulture)),
        long number => new BackupValue(BackupValueKind.Int64, number.ToString(CultureInfo.InvariantCulture)),
        double number => new BackupValue(BackupValueKind.Double, number.ToString(RoundTripNumberFormat, CultureInfo.InvariantCulture)),
        DateTimeOffset moment => new BackupValue(BackupValueKind.DateTimeOffset, moment.ToString(RoundTripDateFormat, CultureInfo.InvariantCulture)),
        DateTime moment => new BackupValue(BackupValueKind.DateTimeOffset, new DateTimeOffset(moment).ToString(RoundTripDateFormat, CultureInfo.InvariantCulture)),
        Guid id => new BackupValue(BackupValueKind.Guid, id.ToString(GuidFormat)),
        BinaryData binary => new BackupValue(BackupValueKind.Binary, Convert.ToBase64String(binary.ToArray())),
        byte[] bytes => new BackupValue(BackupValueKind.Binary, Convert.ToBase64String(bytes)),
        _ => throw new NotSupportedException($"Column '{column}' holds {value.GetType().Name}, which Azure Table Storage cannot store and a backup therefore cannot carry."),
    };

    private static object ToStorageValue(string column, BackupValue value)
    {
        try
        {
            return value.Kind switch
            {
                BackupValueKind.String => value.Value,
                BackupValueKind.Boolean => bool.Parse(value.Value),
                BackupValueKind.Int32 => int.Parse(value.Value, CultureInfo.InvariantCulture),
                BackupValueKind.Int64 => long.Parse(value.Value, CultureInfo.InvariantCulture),
                BackupValueKind.Double => double.Parse(value.Value, CultureInfo.InvariantCulture),
                BackupValueKind.DateTimeOffset => DateTimeOffset.Parse(value.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                BackupValueKind.Guid => Guid.Parse(value.Value),
                BackupValueKind.Binary => Convert.FromBase64String(value.Value),
                _ => throw new BackupFormatException($"Column '{column}' has a value kind this release does not understand."),
            };
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
        {
            throw new BackupFormatException($"Column '{column}' holds '{value.Value}', which is not a valid {value.Kind} value.", exception);
        }
    }
}
