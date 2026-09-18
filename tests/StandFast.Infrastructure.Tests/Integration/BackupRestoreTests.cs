using Azure.Data.Tables;
using StandFast.Application.Backup;
using StandFast.Domain.Entities;
using StandFast.Domain.Enums;
using StandFast.Infrastructure.Storage;

namespace StandFast.Infrastructure.Tests.Integration;

/// <summary>Exports and restores real tables through Azurite, including the guarantee that the audit log is left alone by both.</summary>
public sealed class BackupRestoreTests : IClassFixture<AzuriteTableFixture>
{
    private const string AuditRowKey = "audit-row";

    private readonly AzuriteTableFixture fixture;

    public BackupRestoreTests(AzuriteTableFixture fixture) => this.fixture = fixture;

    [AzuriteFact]
    public async Task Restore_PutsBackExactlyWhatTheBackupHeld()
    {
        (Person person, Standup standup, StandupEntry entry) = await SeedAsync();

        IReadOnlyList<BackupTable> backup = await fixture.Backups.ExportAsync();

        // Change the world in every direction a restore has to undo: a removed row, an edited row, and an added row.
        await fixture.People.DeleteAsync(person.Id);
        standup.Name = "Renamed after the backup";
        await fixture.Standups.UpsertAsync(standup);
        Person added = new() { FirstName = "Added", LastName = "Later", Email = "added.later@example.com", CreatedUtc = DateTimeOffset.UtcNow };
        await fixture.People.UpsertAsync(added);

        IReadOnlyDictionary<string, int> written = await fixture.Backups.ReplaceAsync(backup);

        Person? restoredPerson = await fixture.People.GetAsync(person.Id);
        Standup? restoredStandup = await fixture.Standups.GetAsync(standup.Id);
        StandupEntryPair restoredEntry = await fixture.Entries.GetCurrentAndPriorAsync(standup.Id, person.Id, entry.MeetingDate);

        Assert.Equal(person.Email, restoredPerson?.Email);
        Assert.Equal("Platform daily", restoredStandup?.Name);
        Assert.Null(await fixture.People.GetAsync(added.Id));

        Assert.Equal(AttendanceState.Presented, restoredEntry.Current?.State);
        Assert.Equal(entry.Update, restoredEntry.Current?.Update);
        Assert.Equal(entry.PresentedUtc, restoredEntry.Current?.PresentedUtc);

        Assert.Equal(StorageNames.DataTables.Count, written.Count);
        Assert.Single(await fixture.Standups.GetMembersAsync(standup.Id));
    }

    [AzuriteFact]
    public async Task BackupAndRestore_LeaveTheAuditLogUntouched()
    {
        await SeedAsync();
        await WriteAuditRowAsync();

        IReadOnlyList<BackupTable> backup = await fixture.Backups.ExportAsync();

        Assert.DoesNotContain(backup, table => string.Equals(table.Name, StorageNames.AuditLog, StringComparison.Ordinal));

        await fixture.Backups.ReplaceAsync(backup);

        TableClient auditClient = await fixture.Tables.GetAsync(StorageNames.AuditLog);
        Assert.True((await auditClient.GetEntityIfExistsAsync<TableEntity>(nameof(BackupRestoreTests), AuditRowKey)).HasValue);
    }

    [AzuriteFact]
    public async Task Restore_RefusesARowWithNoKeys()
    {
        BackupTable malformed = new(StorageNames.People, [new Dictionary<string, BackupValue>(StringComparer.Ordinal) { ["FirstName"] = new BackupValue(BackupValueKind.String, "Keyless") }]);

        await Assert.ThrowsAsync<BackupFormatException>(() => fixture.Backups.ReplaceAsync([malformed]));
    }

    private async Task<(Person Person, Standup Standup, StandupEntry Entry)> SeedAsync()
    {
        Person person = new() { FirstName = "Grace", LastName = "Hopper", Email = "grace.hopper@example.com", Notes = "Prefers mornings.", CreatedUtc = DateTimeOffset.UtcNow };
        Standup standup = new() { Name = "Platform daily", RecurrenceDays = MeetingDays.Weekdays, StartTimeLocal = new TimeOnly(9, 15), TimeZoneId = "UTC", CreatedUtc = DateTimeOffset.UtcNow };
        StandupEntry entry = new()
        {
            StandupId = standup.Id,
            PersonId = person.Id,
            MeetingDate = new DateOnly(2026, 9, 15),
            State = AttendanceState.Presented,
            MarkedAvailableUtc = new DateTimeOffset(2026, 9, 15, 9, 15, 0, TimeSpan.Zero),
            PresentedUtc = new DateTimeOffset(2026, 9, 15, 9, 18, 42, TimeSpan.Zero),
            Update = "Shipped the compiler.",
        };

        await fixture.People.UpsertAsync(person);
        await fixture.Standups.UpsertAsync(standup);
        await fixture.Standups.UpsertMemberAsync(new StandupMember { StandupId = standup.Id, PersonId = person.Id, DisplayOrder = 10 });
        await fixture.Entries.UpsertAsync(entry);

        return (person, standup, entry);
    }

    /// <summary>Writes straight to the audit table, because the repositories never touch it: Serilog is its only writer in the running app.</summary>
    private async Task WriteAuditRowAsync()
    {
        TableClient client = await fixture.Tables.GetAsync(StorageNames.AuditLog);
        await client.UpsertEntityAsync(new TableEntity(nameof(BackupRestoreTests), AuditRowKey) { ["Message"] = "Recorded before the restore." });
    }
}
