namespace StandFast.Application.Auditing;

/// <summary>Audit event names. Referenced by both the writers and any query/report over the audit table, so the strings exist once.</summary>
public static class AuditEvents
{
    public const string PersonCreated = "Person.Created";
    public const string PersonUpdated = "Person.Updated";
    public const string PersonDeleted = "Person.Deleted";
    public const string StandupCreated = "Standup.Created";
    public const string StandupUpdated = "Standup.Updated";
    public const string StandupDeleted = "Standup.Deleted";
    public const string MemberAdded = "Standup.MemberAdded";
    public const string MemberRemoved = "Standup.MemberRemoved";
    public const string AttendanceChanged = "Board.AttendanceChanged";
    public const string LeaderChanged = "Board.LeaderChanged";
    public const string UpdateSaved = "Board.UpdateSaved";
    public const string PriorUpdateCopied = "Board.PriorUpdateCopied";
    public const string BackupCreated = "Backup.Created";
    public const string BackupRestored = "Backup.Restored";
}

/// <summary>Structured-log property names attached to every audit record. The Serilog audit sub-logger filters and columnises on these.</summary>
public static class AuditProperties
{
    /// <summary>Marker property whose presence routes an event to the audit sink.</summary>
    public const string IsAudit = "IsAuditEvent";

    public const string EventName = "AuditEvent";
    public const string ActorId = "AuditActorId";
    public const string ActorName = "AuditActorName";
    public const string TargetType = "AuditTargetType";
    public const string TargetId = "AuditTargetId";
}
