namespace StandFast.Application.Dtos;

/// <summary>What a completed restore put back, shown on the backup screen so the outcome is visible rather than assumed.</summary>
public sealed record RestoreSummaryDto(DateTimeOffset BackupCreatedUtc, IReadOnlyList<RestoredTableDto> Tables)
{
    public int RowCount => Tables.Sum(table => table.RowCount);
}

public sealed record RestoredTableDto(string TableName, int RowCount);
