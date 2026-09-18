using StandFast.Application.Backup;

namespace StandFast.Application.Abstractions;

/// <summary>
/// Reads and replaces the raw contents of the tables holding application data. Implemented in Infrastructure, because only that layer knows what
/// the tables are. The audit table is deliberately outside the contract; see <c>StorageNames.DataTables</c> for why.
/// </summary>
public interface IBackupStore
{
    /// <summary>Logical names of the tables a backup covers, in the order it writes them. A restore refuses any other table name.</summary>
    IReadOnlyList<string> TableNames { get; }

    Task<IReadOnlyList<BackupTable>> ExportAsync(CancellationToken cancellationToken = default);

    /// <summary>Clears every table in <see cref="TableNames"/> and writes the supplied rows in their place, returning the row count written per table.</summary>
    Task<IReadOnlyDictionary<string, int>> ReplaceAsync(IReadOnlyList<BackupTable> tables, CancellationToken cancellationToken = default);
}
