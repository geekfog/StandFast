namespace StandFast.Infrastructure.Configuration;

/// <summary>Azure SignalR Service settings, bound from the <see cref="SectionName"/> configuration section, for keeping open boards in step across server copies.</summary>
public sealed class AzureSignalROptions
{
    public const string SectionName = "AzureSignalR";

    /// <summary>
    /// Connection string of a SignalR Service in Serverless mode, or the local emulator. In Azure it names the managed identity instead of a key, for example
    /// <c>Endpoint=https://name.service.signalr.net;AuthType=azure.msi;ClientId=...;Version=1.0;</c>. Empty keeps board changes on the server copy that saved them.
    /// </summary>
    public string? ConnectionString { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);
}
