namespace StandFast.Infrastructure.Storage;

/// <summary>Logical table names. Combined with the configured prefix by <see cref="TableClientProvider"/>; nothing else builds a table name.</summary>
public static class StorageNames
{
    public const string People = "People";
    public const string Standups = "Standups";
    public const string StandupMembers = "StandupMembers";
    public const string StandupEntries = "StandupEntries";
    public const string AuditLog = "AuditLog";
}
