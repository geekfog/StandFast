using Azure.Data.Tables;
using Azure.Identity;
using StandFast.Infrastructure.Configuration;

namespace StandFast.Infrastructure.Storage;

/// <summary>
/// The one place a <see cref="TableServiceClient"/> is constructed. Dependency injection and the Serilog audit sink both come through here,
/// so the choice between a managed identity and a connection string is made once.
/// </summary>
public static class TableServiceClientFactory
{
    /// <summary>
    /// A configured service URI wins and authenticates with <see cref="DefaultAzureCredential"/>, which resolves the container app's managed
    /// identity in Azure and the developer sign-in locally. Otherwise the connection string is used, which is how Azurite is reached.
    /// </summary>
    public static TableServiceClient Create(TableStorageOptions options) =>
        options.UsesManagedIdentity
            ? new TableServiceClient(new Uri(options.ServiceUri!), new DefaultAzureCredential())
            : new TableServiceClient(options.ConnectionString);
}
