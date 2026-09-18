namespace StandFast.Application.Backup;

/// <summary>
/// A whole backup: every row of every application data table, as columns rather than as typed entities. Keeping the shape generic means a new
/// table or a new column is included without touching the backup code, and it keeps the storage layer's entity classes out of the file format.
/// </summary>
public sealed record BackupDocument(int FormatVersion, DateTimeOffset CreatedUtc, string Application, IReadOnlyList<BackupTable> Tables)
{
    public int RowCount => Tables.Sum(table => table.Rows.Count);
}

/// <summary>One table's rows. <see cref="Name"/> is the logical table name, without the environment's table prefix, so a backup restores into any environment.</summary>
public sealed record BackupTable(string Name, IReadOnlyList<IReadOnlyDictionary<string, BackupValue>> Rows);

/// <summary>
/// A single column value. Azure Table Storage is typed, so the type travels with the value: writing a date back as a string would store the wrong
/// column type and break every reader of that row.
/// </summary>
public sealed record BackupValue(BackupValueKind Kind, string Value);

/// <summary>The column types Azure Table Storage supports. Anything outside this set cannot be stored, so it cannot appear in a backup either.</summary>
public enum BackupValueKind
{
    String,
    Boolean,
    Int32,
    Int64,
    Double,
    DateTimeOffset,
    Guid,
    Binary,
}
