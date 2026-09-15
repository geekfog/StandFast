using Serilog;
using Serilog.Filters;
using StandFast.Application.Auditing;
using StandFast.Infrastructure.Configuration;
using StandFast.Infrastructure.DependencyInjection;
using StandFast.Infrastructure.Storage;

namespace StandFast.Ui.Services;

/// <summary>Wires Serilog for the app: general logs go wherever configuration says, and audit records additionally fan out to their own Azure Table.</summary>
public static class LoggingSetup
{
    /// <summary>Rolling log file written during local development only. Containers log to stdout, which is what Container Apps forwards to Log Analytics.</summary>
    private const string DevelopmentLogPath = "logs/standfast-.log";

    private const int DevelopmentLogRetainedFiles = 14;

    private const string DevelopmentLogTemplate = "{Timestamp:o} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}";

    /// <summary>Audit properties promoted to real table columns so the audit table is queryable without parsing the message.</summary>
    private static readonly string[] AuditColumns =
    [
        AuditProperties.EventName,
        AuditProperties.ActorId,
        AuditProperties.ActorName,
        AuditProperties.TargetType,
        AuditProperties.TargetId,
    ];

    /// <summary>Minimal logger used before configuration and DI exist, so a failure during startup is still recorded.</summary>
    public static Serilog.ILogger CreateBootstrapLogger() => new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

    public static void UseStandFastSerilog(this WebApplicationBuilder builder)
    {
        // Storage settings are read straight from configuration and the table client is built here. Resolving either through the service provider
        // would mean re-entering the container while it is still being constructed, which hangs host startup.
        TableStorageOptions storage = InfrastructureServiceCollectionExtensions.ReadOptions(builder.Configuration);
        string auditTableName = TableClientProvider.BuildTableName(storage.TablePrefix, StorageNames.AuditLog);

        builder.Host.UseSerilog((context, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .WriteTo.Logger(auditLogger => auditLogger
                    .Filter.ByIncludingOnly(Matching.WithProperty<bool>(AuditProperties.IsAudit, isAudit => isAudit))
                    .WriteTo.AzureTableStorage(TableServiceClientFactory.Create(storage), storageTableName: auditTableName, propertyColumns: AuditColumns));

            // The file sink is added here rather than in appsettings.Development.json: configuration arrays merge by index, so a WriteTo entry
            // in the development overlay replaces the console sink from appsettings.json instead of adding to it.
            if (context.HostingEnvironment.IsDevelopment())
            {
                loggerConfiguration.WriteTo.File(
                    path: DevelopmentLogPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: DevelopmentLogRetainedFiles,
                    outputTemplate: DevelopmentLogTemplate);
            }
        });
    }
}
