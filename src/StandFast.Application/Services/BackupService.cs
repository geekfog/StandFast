using StandFast.Application.Abstractions;
using StandFast.Application.Auditing;
using StandFast.Application.Backup;
using StandFast.Application.Dtos;
using StandFast.Domain.Abstractions;
using StandFast.Domain.Common;

namespace StandFast.Application.Services;

public sealed class BackupService(IBackupStore store, IClock clock, IAuditLog audit) : IBackupService
{
    private const string AuditTargetType = "Backup";

    public async Task<BackupFileDto> CreateAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<BackupTable> tables = await store.ExportAsync(cancellationToken);
        BackupDocument document = new(BackupFormat.CurrentVersion, clock.UtcNow, ApplicationInfo.Name, tables);
        string fileName = BackupFormat.BuildFileName(document.CreatedUtc);

        audit.Record(AuditEvents.BackupCreated, AuditTargetType, fileName, $"{document.RowCount} rows across {tables.Count} tables.");

        return new BackupFileDto(fileName, BackupFormat.ContentType, BackupSerializer.Serialize(document), document.RowCount);
    }

    public async Task<RestoreSummaryDto> RestoreAsync(Stream content, CancellationToken cancellationToken = default)
    {
        BackupDocument document = await BackupSerializer.DeserializeAsync(content, cancellationToken);
        Validate(document);

        IReadOnlyDictionary<string, int> written = await store.ReplaceAsync(document.Tables, cancellationToken);

        // Every table is reported, in the store's own order, including the ones the backup left empty: a restore that emptied a table is exactly
        // what the reader needs to see.
        RestoreSummaryDto summary = new(document.CreatedUtc, [.. store.TableNames.Select(name => new RestoredTableDto(name, written.GetValueOrDefault(name)))]);

        audit.Record(AuditEvents.BackupRestored, AuditTargetType, document.CreatedUtc.ToString("O"), $"{summary.RowCount} rows restored from a backup taken on {document.CreatedUtc:O}.");

        return summary;
    }

    /// <summary>
    /// A restore replaces everything, so the file is checked before any row is touched. An unknown table name is refused rather than ignored:
    /// it means the file came from a different application or a later version, and silently restoring the rest would leave a half-populated system.
    /// </summary>
    private void Validate(BackupDocument document)
    {
        if (document.FormatVersion != BackupFormat.CurrentVersion)
        {
            throw new BackupFormatException($"This backup is format version {document.FormatVersion}, and this release restores version {BackupFormat.CurrentVersion}.");
        }

        string[] unknown = [.. document.Tables.Select(table => table.Name).Where(name => !store.TableNames.Contains(name, StringComparer.Ordinal))];
        if (unknown.Length > 0)
        {
            throw new BackupFormatException($"The backup contains tables this application does not restore: {string.Join(", ", unknown)}.");
        }

        string[] duplicates = [.. document.Tables.GroupBy(table => table.Name, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key)];
        if (duplicates.Length > 0)
        {
            throw new BackupFormatException($"The backup lists the same table more than once: {string.Join(", ", duplicates)}.");
        }
    }
}
