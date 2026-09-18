using StandFast.Application.Abstractions;
using StandFast.Application.Backup;

namespace StandFast.Application.Tests.Fakes;

/// <summary>Stands in for the storage layer so the backup service is tested on its own terms: what it writes, what it refuses, and what it reports.</summary>
public sealed class InMemoryBackupStore(params string[] tableNames) : IBackupStore
{
    public IReadOnlyList<string> TableNames { get; } = tableNames.Length > 0 ? tableNames : [DefaultTableName];

    public const string DefaultTableName = "People";

    public List<BackupTable> Contents { get; } = [];

    public Task<IReadOnlyList<BackupTable>> ExportAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<BackupTable>>([.. Contents]);

    public Task<IReadOnlyDictionary<string, int>> ReplaceAsync(IReadOnlyList<BackupTable> tables, CancellationToken cancellationToken = default)
    {
        Contents.Clear();
        Contents.AddRange(TableNames.Select(name => new BackupTable(name, tables.FirstOrDefault(table => table.Name == name)?.Rows ?? [])));

        return Task.FromResult<IReadOnlyDictionary<string, int>>(Contents.ToDictionary(table => table.Name, table => table.Rows.Count, StringComparer.Ordinal));
    }
}
