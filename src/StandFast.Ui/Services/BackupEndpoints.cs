using StandFast.Application.Dtos;
using StandFast.Application.Services;
using StandFast.Ui.Common;

namespace StandFast.Ui.Services;

/// <summary>
/// The backup download. It is an endpoint rather than something the Blazor circuit does, because a download is an HTTP response with a content type
/// and a file name, and streaming it this way keeps the file out of circuit memory. It carries no authorisation metadata, so the fallback policy
/// protects it exactly like a page.
/// </summary>
public static class BackupEndpoints
{
    public static void MapStandFastBackup(this WebApplication app) =>
        app.MapGet(UiRoutes.BackupDownload, async (IBackupService backups, CancellationToken cancellationToken) =>
        {
            BackupFileDto file = await backups.CreateAsync(cancellationToken);
            return Results.File(file.Content, file.ContentType, file.FileName);
        });
}
