using System.Globalization;
using StandFast.Domain.Common;

namespace StandFast.Application.Backup;

/// <summary>Everything about the backup file itself: its version, its name, its media type, and the largest upload a restore accepts.</summary>
public static class BackupFormat
{
    /// <summary>Raised when the document shape changes in a way an older reader cannot handle. A restore refuses a version it does not know rather than guessing.</summary>
    public const int CurrentVersion = 1;

    public const string ContentType = "application/json";

    public const string FileExtension = ".json";

    private const string FileNameSuffix = "-Backup-";

    private const string FileNameTimestampFormat = "yyyyMMdd-HHmmss";

    /// <summary>
    /// Ceiling on an uploaded backup, so a mistaken upload is rejected before it is buffered. The tables hold a few small rows per person per day,
    /// so a real backup is orders of magnitude smaller than this.
    /// </summary>
    public const long MaxFileBytes = 64 * 1024 * 1024;

    public static string BuildFileName(DateTimeOffset createdUtc) =>
        string.Concat(ApplicationInfo.Name, FileNameSuffix, createdUtc.ToString(FileNameTimestampFormat, CultureInfo.InvariantCulture), FileExtension);
}
