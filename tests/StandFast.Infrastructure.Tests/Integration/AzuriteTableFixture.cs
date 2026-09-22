using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;
using StandFast.Infrastructure.Backup;
using StandFast.Infrastructure.Configuration;
using StandFast.Infrastructure.Repositories;
using StandFast.Infrastructure.Storage;

namespace StandFast.Infrastructure.Tests.Integration;

/// <summary>Gives each test class its own table prefix against Azurite, and drops those tables afterwards so runs never see each other's rows.</summary>
public sealed class AzuriteTableFixture : IDisposable
{
    private readonly TableServiceClient serviceClient;

    public AzuriteTableFixture()
    {
        // The prefix must satisfy the options regex: a letter followed by at most 20 alphanumeric characters.
        string prefix = $"T{Guid.NewGuid():N}"[..21];

        serviceClient = new TableServiceClient(AzuriteEndpoint.ConnectionString);
        Tables = new TableClientProvider(serviceClient, Options.Create(new TableStorageOptions { ConnectionString = AzuriteEndpoint.ConnectionString, TablePrefix = prefix }));

        People = new PersonRepository(Tables);
        Preferences = new UserPreferencesRepository(Tables);
        Standups = new StandupRepository(Tables);
        Entries = new StandupEntryRepository(Tables);
        Backups = new TableBackupStore(Tables);
    }

    public ITableClientProvider Tables { get; }

    public PersonRepository People { get; }

    public UserPreferencesRepository Preferences { get; }

    public StandupRepository Standups { get; }

    public StandupEntryRepository Entries { get; }

    public TableBackupStore Backups { get; }

    /// <summary>
    /// xUnit builds and disposes a class fixture even when every test in the class is skipped, so cleanup has to cope with the emulator being
    /// absent. Without this guard a machine with no Azurite reports class cleanup failures alongside the skips.
    /// </summary>
    public void Dispose()
    {
        if (!AzuriteEndpoint.IsAvailable)
        {
            return;
        }

        foreach (string logicalName in StorageNames.AllTables)
        {
            try
            {
                serviceClient.DeleteTable(Tables.ResolveTableName(logicalName));
            }
            catch (RequestFailedException)
            {
                // A table this class never touched was never created. Nothing to clean up.
            }
        }
    }
}
