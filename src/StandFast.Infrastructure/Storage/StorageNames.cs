namespace StandFast.Infrastructure.Storage;

/// <summary>Logical table names. Combined with the configured prefix by <see cref="TableClientProvider"/>; nothing else builds a table name.</summary>
public static class StorageNames
{
    public const string People = "People";
    public const string Standups = "Standups";
    public const string StandupMembers = "StandupMembers";
    public const string StandupEntries = "StandupEntries";
    public const string UserPreferences = "UserPreferences";
    public const string AuditLog = "AuditLog";

    /// <summary>
    /// The tables holding application data, in the order a backup writes them and a restore replaces them. <see cref="AuditLog"/> is deliberately
    /// absent: Serilog owns it, it is the append-only record of who did what, and restoring an older copy over it would erase the trail that
    /// explains the restore itself. <see cref="UserPreferences"/> is absent for a related reason: it belongs to the people using the app rather
    /// than to the board data, so restoring last month's copy of the board leaves everyone's own settings alone.
    /// </summary>
    public static readonly IReadOnlyList<string> DataTables = [People, Standups, StandupMembers, StandupEntries];

    /// <summary>Every table the app creates. Tests clean up from this list, so a table added above is never left behind.</summary>
    public static readonly IReadOnlyList<string> AllTables = [.. DataTables, UserPreferences, AuditLog];
}
