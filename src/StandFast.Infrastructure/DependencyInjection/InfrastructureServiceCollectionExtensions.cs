using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StandFast.Domain.Abstractions;
using StandFast.Infrastructure.Configuration;
using StandFast.Infrastructure.Repositories;
using StandFast.Infrastructure.Storage;
using StandFast.Infrastructure.Time;

namespace StandFast.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>Registers Azure Table Storage persistence. Connection details come from the <see cref="TableStorageOptions.SectionName"/> configuration section.</summary>
    public static IServiceCollection AddStandFastInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TableStorageOptions>()
            .Bind(configuration.GetSection(TableStorageOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => options.UsesManagedIdentity || !string.IsNullOrWhiteSpace(options.ConnectionString), $"Configure either {TableStorageOptions.SectionName}:ServiceUri or {TableStorageOptions.SectionName}:ConnectionString.")
            .ValidateOnStart();

        // The client is built from the bound configuration rather than resolved through an Azure client factory, because the logging setup needs
        // the same client while the container is still being built and a re-entrant resolve at that point deadlocks host startup.
        services.AddSingleton(_ => TableServiceClientFactory.Create(ReadOptions(configuration)));

        services.AddSingleton<ITableClientProvider, TableClientProvider>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IPersonRepository, PersonRepository>();
        services.AddScoped<IStandupRepository, StandupRepository>();
        services.AddScoped<IStandupEntryRepository, StandupEntryRepository>();

        return services;
    }

    /// <summary>Binds the storage options straight from configuration, for callers that run before the service provider exists.</summary>
    public static TableStorageOptions ReadOptions(IConfiguration configuration) =>
        configuration.GetSection(TableStorageOptions.SectionName).Get<TableStorageOptions>() ?? new TableStorageOptions();
}
