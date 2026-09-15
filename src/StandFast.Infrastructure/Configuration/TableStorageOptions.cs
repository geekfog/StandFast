using System.ComponentModel.DataAnnotations;

namespace StandFast.Infrastructure.Configuration;

/// <summary>Azure Table Storage connection settings, bound from the <see cref="SectionName"/> configuration section.</summary>
public sealed class TableStorageOptions
{
    public const string SectionName = "AzureTableStorage";

    /// <summary>Table service endpoint, for example <c>https://mystorage.table.core.windows.net</c>. Preferred in Azure: it is used with a managed identity and no secret.</summary>
    public string? ServiceUri { get; set; }

    /// <summary>Connection string fallback for local development (Azurite) or any environment without a managed identity.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Prefix applied to every table name, so several environments can share one storage account.</summary>
    [RegularExpression("^[A-Za-z][A-Za-z0-9]{0,20}$", ErrorMessage = "Table prefix must start with a letter and contain only letters and digits.")]
    public string TablePrefix { get; set; } = "StandFast";

    /// <summary>Creates any missing tables at startup. Turn off where the identity has no table-create permission and the tables are provisioned by infrastructure.</summary>
    public bool CreateTablesOnStartup { get; set; } = true;

    public bool UsesManagedIdentity => !string.IsNullOrWhiteSpace(ServiceUri);
}
