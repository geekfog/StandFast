using StandFast.Application.Backup;
using StandFast.Application.Dtos;
using StandFast.Application.Services;
using StandFast.Application.Tests.Fakes;

namespace StandFast.Application.Tests.Backup;

/// <summary>Covers the round trip through the file format and the checks a restore makes before it replaces anything.</summary>
public sealed class BackupServiceTests
{
    private const string SecondTableName = "Standups";

    private static readonly DateTimeOffset Now = new(2026, 9, 18, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Backup_RoundTripsEveryColumnTypeAndRestoresIt()
    {
        InMemoryBackupStore store = new(InMemoryBackupStore.DefaultTableName, SecondTableName);
        store.Contents.Add(new BackupTable(InMemoryBackupStore.DefaultTableName, [BuildRow()]));
        store.Contents.Add(new BackupTable(SecondTableName, []));

        BackupService service = BuildService(store);
        BackupFileDto file = await service.CreateAsync();

        Assert.Equal(1, file.RowCount);
        Assert.EndsWith(BackupFormat.FileExtension, file.FileName, StringComparison.Ordinal);

        // Emptying the store first proves the rows come back out of the file rather than being left in place.
        store.Contents.Clear();
        RestoreSummaryDto summary = await service.RestoreAsync(new MemoryStream(file.Content));

        Assert.Equal(1, summary.RowCount);
        Assert.Equal(store.TableNames, [.. summary.Tables.Select(table => table.TableName)]);

        IReadOnlyDictionary<string, BackupValue> restored = Assert.Single(store.Contents.Single(table => table.Name == InMemoryBackupStore.DefaultTableName).Rows);
        Assert.Equal(BuildRow(), restored);
    }

    [Fact]
    public async Task Restore_ReportsEveryTableIncludingTheOnesTheBackupEmptied()
    {
        InMemoryBackupStore store = new(InMemoryBackupStore.DefaultTableName, SecondTableName);
        store.Contents.Add(new BackupTable(InMemoryBackupStore.DefaultTableName, [BuildRow()]));

        BackupService service = BuildService(store);
        BackupFileDto file = await service.CreateAsync();
        RestoreSummaryDto summary = await service.RestoreAsync(new MemoryStream(file.Content));

        Assert.Equal(0, summary.Tables.Single(table => table.TableName == SecondTableName).RowCount);
    }

    [Fact]
    public async Task Restore_RecordsBothTheBackupAndTheRestoreInTheAuditLog()
    {
        InMemoryBackupStore store = new();
        RecordingAuditLog audit = new();
        BackupService service = BuildService(store, audit);

        BackupFileDto file = await service.CreateAsync();
        await service.RestoreAsync(new MemoryStream(file.Content));

        Assert.Collection(
            audit.Records,
            record => Assert.Equal(Application.Auditing.AuditEvents.BackupCreated, record.EventName),
            record => Assert.Equal(Application.Auditing.AuditEvents.BackupRestored, record.EventName));
    }

    [Fact]
    public async Task Restore_RefusesATableTheApplicationDoesNotOwn()
    {
        InMemoryBackupStore store = new();
        BackupService service = BuildService(store);
        byte[] content = BackupSerializer.Serialize(new BackupDocument(BackupFormat.CurrentVersion, Now, "StandFast", [new BackupTable("AuditLog", [BuildRow()])]));

        BackupFormatException exception = await Assert.ThrowsAsync<BackupFormatException>(() => service.RestoreAsync(new MemoryStream(content)));

        Assert.Contains("AuditLog", exception.Message, StringComparison.Ordinal);
        Assert.Empty(store.Contents);
    }

    [Fact]
    public async Task Restore_RefusesAFormatVersionItDoesNotKnow()
    {
        BackupService service = BuildService(new InMemoryBackupStore());
        byte[] content = BackupSerializer.Serialize(new BackupDocument(BackupFormat.CurrentVersion + 1, Now, "StandFast", []));

        await Assert.ThrowsAsync<BackupFormatException>(() => service.RestoreAsync(new MemoryStream(content)));
    }

    [Fact]
    public async Task Restore_RefusesAFileThatIsNotABackup()
    {
        BackupService service = BuildService(new InMemoryBackupStore());

        await Assert.ThrowsAsync<BackupFormatException>(() => service.RestoreAsync(new MemoryStream("not json"u8.ToArray())));
    }

    private static BackupService BuildService(InMemoryBackupStore store, RecordingAuditLog? audit = null) => new(store, new FixedClock(Now), audit ?? new RecordingAuditLog());

    /// <summary>One row using every supported column type, so the serializer is exercised across all of them rather than on strings alone.</summary>
    private static Dictionary<string, BackupValue> BuildRow() => new(StringComparer.Ordinal)
    {
        ["PartitionKey"] = new BackupValue(BackupValueKind.String, "Person"),
        ["RowKey"] = new BackupValue(BackupValueKind.String, "0199b0c0-0000-7000-8000-000000000001"),
        ["IsActive"] = new BackupValue(BackupValueKind.Boolean, "True"),
        ["DisplayOrder"] = new BackupValue(BackupValueKind.Int32, "20"),
        ["Ticks"] = new BackupValue(BackupValueKind.Int64, "9007199254740993"),
        ["Weight"] = new BackupValue(BackupValueKind.Double, "0.1"),
        ["CreatedUtc"] = new BackupValue(BackupValueKind.DateTimeOffset, "2026-09-18T08:30:00.0000000+00:00"),
        ["StandupId"] = new BackupValue(BackupValueKind.Guid, "0199b0c0-0000-7000-8000-000000000002"),
        ["Payload"] = new BackupValue(BackupValueKind.Binary, "U3RhbmRGYXN0"),
    };
}
