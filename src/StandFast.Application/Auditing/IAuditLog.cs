namespace StandFast.Application.Auditing;

/// <summary>Writes an attributed, structured audit record. One call site shape for every auditable action, so the record layout never drifts between features.</summary>
public interface IAuditLog
{
    void Record(string eventName, string targetType, string targetId, string summary);
}
