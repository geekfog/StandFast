using StandFast.Application.Dtos;

namespace StandFast.Application.Services;

public interface IBackupService
{
    /// <summary>Exports every application data table into a single downloadable file. The audit log is not included.</summary>
    Task<BackupFileDto> CreateAsync(CancellationToken cancellationToken = default);

    /// <summary>Replaces all application data with the contents of a backup file. Existing rows are removed first, so the result is the backup and nothing else.</summary>
    Task<RestoreSummaryDto> RestoreAsync(Stream content, CancellationToken cancellationToken = default);
}
