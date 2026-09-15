using System.Net.Sockets;

namespace StandFast.Infrastructure.Tests.Integration;

/// <summary>
/// Marks a test that needs the Azurite storage emulator. When Azurite is not listening the test is skipped rather than failed,
/// so the suite stays green on a machine or build agent that has no emulator.
/// </summary>
public sealed class AzuriteFactAttribute : FactAttribute
{
    public AzuriteFactAttribute()
    {
        if (!AzuriteEndpoint.IsAvailable)
        {
            Skip = $"Azurite is not listening on {AzuriteEndpoint.Host}:{AzuriteEndpoint.TablePort}.";
        }
    }
}

public static class AzuriteEndpoint
{
    public const string Host = "127.0.0.1";
    public const int TablePort = 10002;
    public const string ConnectionString = "UseDevelopmentStorage=true";

    private const int ProbeTimeoutMilliseconds = 1500;

    private static readonly Lazy<bool> Probe = new(() =>
    {
        try
        {
            using TcpClient client = new();
            return client.ConnectAsync(Host, TablePort).Wait(ProbeTimeoutMilliseconds) && client.Connected;
        }
        catch (SocketException)
        {
            return false;
        }
    });

    public static bool IsAvailable => Probe.Value;
}
