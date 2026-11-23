namespace StandFast.Infrastructure.Tests.Integration;

/// <summary>Marks a test that needs the Azure SignalR emulator. When the emulator is not listening the test is skipped rather than failed.</summary>
public sealed class SignalREmulatorFactAttribute : FactAttribute
{
    public SignalREmulatorFactAttribute()
    {
        if (!SignalREmulatorEndpoint.IsAvailable)
        {
            Skip = $"The Azure SignalR emulator is not listening on {AzuriteEndpoint.Host}:{SignalREmulatorEndpoint.Port}.";
        }
    }
}

public static class SignalREmulatorEndpoint
{
    public const int Port = 8888;

    /// <summary>The emulator's fixed, publicly documented access key, the same one appsettings.Development.json uses.</summary>
    public const string ConnectionString = "Endpoint=http://localhost;Port=8888;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGH;Version=1.0;";

    private static readonly Lazy<bool> Probe = new(() => AzuriteEndpoint.CanConnect(AzuriteEndpoint.Host, Port));

    public static bool IsAvailable => Probe.Value;
}
