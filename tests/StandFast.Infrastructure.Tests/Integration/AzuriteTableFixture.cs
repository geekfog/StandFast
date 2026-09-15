using Azure.Data.Tables;
using Microsoft.Extensions.Options;
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
        Standups = new StandupRepository(Tables);
        Entries = new StandupEntryRepository(Tables);
    }

    public ITableClientProvider Tables { get; }

    public PersonRepository People { get; }

    public StandupRepository Standups { get; }

    public StandupEntryRepository Entries { get; }

    public void Dispose()
    {
        foreach (string logicalName in new[] { StorageNames.People, StorageNames.Standups, StorageNames.StandupMembers, StorageNames.StandupEntries })
        {
            serviceClient.DeleteTable(Tables.ResolveTableName(logicalName));
        }
    }
}
