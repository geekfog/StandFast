namespace StandFast.Application.Backup;

/// <summary>Thrown when an uploaded file is not a backup this version can restore. The restore screen shows the message, so it is written for a person to read.</summary>
public sealed class BackupFormatException(string message, Exception? innerException = null) : Exception(message, innerException);
