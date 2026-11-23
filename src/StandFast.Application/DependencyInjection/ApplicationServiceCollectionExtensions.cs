using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StandFast.Application.Abstractions;
using StandFast.Application.Auditing;
using StandFast.Application.Configuration;
using StandFast.Application.Notifications;
using StandFast.Application.Services;

namespace StandFast.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Registers application services and every validator in this assembly. Callers supply the repository, clock, and current-user implementations.</summary>
    public static IServiceCollection AddStandFastApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatorsFromAssemblyContaining<ApplicationAssemblyMarker>(ServiceLifetime.Singleton);

        services.AddOptions<BoardLockOptions>()
            .Bind(configuration.GetSection(BoardLockOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Infrastructure replaces the transport when a cross-server channel is configured.
        services.TryAddSingleton<IBoardChangeTransport, LocalOnlyBoardChangeTransport>();
        services.AddSingleton<BoardChangeBus>();
        services.AddScoped<IBoardChangeNotifier, BoardChangeNotifier>();

        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<IPersonService, PersonService>();
        services.AddScoped<IStandupService, StandupService>();
        services.AddScoped<IBoardService, BoardService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<IUserPreferencesService, UserPreferencesService>();
        services.AddScoped<IHolidayService, HolidayService>();
        services.AddScoped<ISprintService, SprintService>();
        services.AddScoped<IDayOffService, DayOffService>();
        services.AddScoped<ITaskStateService, TaskStateService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IOrganizationSettingsService, OrganizationSettingsService>();

        return services;
    }
}

/// <summary>Assembly anchor for validator scanning.</summary>
public sealed class ApplicationAssemblyMarker;
