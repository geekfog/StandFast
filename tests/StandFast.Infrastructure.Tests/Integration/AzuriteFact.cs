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

    private static readonly Lazy<bool> Probe = new(() => CanConnect(Host, TablePort));

    public static bool IsAvailable => Probe.Value;

    /// <summary>
    /// True when something is listening on the port. This must never throw, for two reasons: it runs inside an attribute constructor, where an
    /// exception becomes an xUnit discovery failure for the whole class rather than a skip, and <see cref="Lazy{T}"/> caches a thrown exception and
    /// rethrows it on every later call. A closed port refuses the connection outright rather than timing out, and <c>Wait</c> reports that as an
    /// <see cref="AggregateException"/> wrapping the socket error, so catching only <see cref="SocketException"/> lets it through.
    /// </summary>
    internal static bool CanConnect(string host, int port)
    {
        try
        {
            using TcpClient client = new();
            return client.ConnectAsync(host, port).Wait(ProbeTimeoutMilliseconds) && client.Connected;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
