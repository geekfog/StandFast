using Microsoft.Extensions.Logging;
using StandFast.Application.Abstractions;

namespace StandFast.Application.Auditing;

/// <summary>Emits audit records through <see cref="ILogger"/> so Serilog's sink configuration (console, file, Azure Table) decides where they land.</summary>
public sealed class AuditLog(ILogger<AuditLog> logger, ICurrentUser currentUser) : IAuditLog
{
    private const string AnonymousActor = "(unauthenticated)";

    public void Record(string eventName, string targetType, string targetId, string summary)
    {
        using IDisposable? scope = logger.BeginScope(new Dictionary<string, object?>
        {
            [AuditProperties.IsAudit] = true,
            [AuditProperties.EventName] = eventName,
            [AuditProperties.ActorId] = currentUser.UserId,
            [AuditProperties.ActorName] = currentUser.DisplayName ?? currentUser.Email ?? AnonymousActor,
            [AuditProperties.TargetType] = targetType,
            [AuditProperties.TargetId] = targetId,
        });

        logger.LogInformation("{AuditEventName} by {AuditActor} on {AuditTarget}: {AuditSummary}", eventName, currentUser.DisplayName ?? currentUser.Email ?? AnonymousActor, $"{targetType}/{targetId}", summary);
    }
}
