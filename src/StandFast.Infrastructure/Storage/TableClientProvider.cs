using System.Collections.Concurrent;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;
using StandFast.Infrastructure.Configuration;

namespace StandFast.Infrastructure.Storage;

/// <summary>Resolves a ready-to-use <see cref="TableClient"/> for a logical table name, applying the configured prefix and creating the table once per process.</summary>
public interface ITableClientProvider
{
    Task<TableClient> GetAsync(string logicalTableName, CancellationToken cancellationToken = default);

    /// <summary>Physical table name for a logical name, including the configured prefix. Used by the logging setup, which configures its sink before DI exists.</summary>
    string ResolveTableName(string logicalTableName);
}

public sealed class TableClientProvider(TableServiceClient serviceClient, IOptions<TableStorageOptions> options) : ITableClientProvider
{
    private readonly TableStorageOptions options = options.Value;
    private readonly ConcurrentDictionary<string, Task<TableClient>> clients = new(StringComparer.Ordinal);

    public string ResolveTableName(string logicalTableName) => BuildTableName(options.TablePrefix, logicalTableName);

    /// <summary>Table names allow only letters and digits, so the prefix is concatenated directly. Shared with logging configuration so both agree on the physical name.</summary>
    public static string BuildTableName(string prefix, string logicalTableName) => string.Concat(prefix, logicalTableName);

    public Task<TableClient> GetAsync(string logicalTableName, CancellationToken cancellationToken = default) =>
        clients.GetOrAdd(logicalTableName, name => CreateAsync(name, cancellationToken));

    private async Task<TableClient> CreateAsync(string logicalTableName, CancellationToken cancellationToken)
    {
        TableClient client = serviceClient.GetTableClient(ResolveTableName(logicalTableName));

        if (options.CreateTablesOnStartup)
        {
            await client.CreateIfNotExistsAsync(cancellationToken);
        }

        return client;
    }
}
