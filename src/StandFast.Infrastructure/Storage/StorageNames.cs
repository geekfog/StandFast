namespace StandFast.Infrastructure.Storage;

/// <summary>Logical table names. Combined with the configured prefix by <see cref="TableClientProvider"/>; nothing else builds a table name.</summary>
public static class StorageNames
{
    public const string People = "People";
    public const string Standups = "Standups";
    public const string StandupMembers = "StandupMembers";
    public const string StandupEntries = "StandupEntries";
    public const string AuditLog = "AuditLog";

    /// <summary>
    /// The tables holding application data, in the order a backup writes them and a restore replaces them. <see cref="AuditLog"/> is deliberately
    /// absent: Serilog owns it, it is the append-only record of who did what, and restoring an older copy over it would erase the trail that
    /// explains the restore itself.
    /// </summary>
    public static readonly IReadOnlyList<string> DataTables = [People, Standups, StandupMembers, StandupEntries];
}
