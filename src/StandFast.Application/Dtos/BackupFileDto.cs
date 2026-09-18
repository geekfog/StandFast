namespace StandFast.Application.Dtos;

/// <summary>A generated backup, ready to be sent to the browser as a download.</summary>
public sealed record BackupFileDto(string FileName, string ContentType, byte[] Content, int RowCount);
