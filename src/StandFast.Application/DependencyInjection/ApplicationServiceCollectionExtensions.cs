using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using StandFast.Application.Auditing;
using StandFast.Application.Services;

namespace StandFast.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Registers application services and every validator in this assembly. Callers supply the repository, clock, and current-user implementations.</summary>
    public static IServiceCollection AddStandFastApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<ApplicationAssemblyMarker>(ServiceLifetime.Singleton);

        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<IPersonService, PersonService>();
        services.AddScoped<IStandupService, StandupService>();
        services.AddScoped<IBoardService, BoardService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<IUserPreferencesService, UserPreferencesService>();

        return services;
    }
}

/// <summary>Assembly anchor for validator scanning.</summary>
public sealed class ApplicationAssemblyMarker;
